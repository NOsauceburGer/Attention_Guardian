using System.Text.Json;
using AttentionGuardian.Application;
using AttentionGuardian.Core;

namespace AttentionGuardian.SharedVectors.Tests;

public sealed class VectorContractTests
{
    [Fact]
    public void VersionOneDirectory_ContainsSchemaAndOperationFiles()
    {
        var directory = VectorPaths.VersionOneDirectory;

        Assert.True(Directory.Exists(directory), $"Missing vector directory: {directory}");
        Assert.True(File.Exists(Path.Combine(directory, "schema.json")));

        var operationFiles = Directory
            .EnumerateFiles(directory, "*.json")
            .Where(path => Path.GetFileName(path) != "schema.json")
            .ToArray();

        Assert.NotEmpty(operationFiles);
    }

    [Fact]
    public void EveryOperationFile_UsesVersionOneEnvelope()
    {
        var operationFiles = Directory
            .EnumerateFiles(VectorPaths.VersionOneDirectory, "*.json")
            .Where(path => Path.GetFileName(path) != "schema.json");

        foreach (var path in operationFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("operation").GetString()));
            Assert.NotEmpty(root.GetProperty("cases").EnumerateArray());
        }
    }

    [Fact]
    public void Schema_DeclaresEveryOperationFileAndOperationSpecificCaseDefinition()
    {
        using var schema = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(VectorPaths.VersionOneDirectory, "schema.json")));
        var root = schema.RootElement;
        var declaredOperations = root
            .GetProperty("properties")
            .GetProperty("operation")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .Order()
            .ToArray();
        var fileOperations = Directory
            .EnumerateFiles(VectorPaths.VersionOneDirectory, "*.json")
            .Where(path => Path.GetFileName(path) != "schema.json")
            .Select(path => VectorFile.Parse(File.ReadAllText(path)).Operation)
            .Order()
            .ToArray();
        var operationDefinitions = root.GetProperty("allOf").EnumerateArray()
            .Select(rule => rule
                .GetProperty("if")
                .GetProperty("properties")
                .GetProperty("operation")
                .GetProperty("const")
                .GetString()!)
            .Order()
            .ToArray();

        Assert.Equal(declaredOperations, fileOperations);
        Assert.Equal(declaredOperations, operationDefinitions);
    }

    [Fact]
    public void UnknownSchemaVersion_IsRejected()
    {
        const string json = """{"schemaVersion":2,"operation":"selectCurrent","cases":[]}""";

        var exception = Assert.Throws<VectorFormatException>(() => VectorFile.Parse(json));

        Assert.Contains("Unsupported schemaVersion 2", exception.Message);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"schemaVersion":1,"cases":[]}""")]
    [InlineData("""{"schemaVersion":1,"operation":"unknown","cases":[]}""")]
    public void MalformedEnvelope_IsRejected(string json)
    {
        Assert.Throws<VectorFormatException>(() => VectorFile.Parse(json));
    }

    [Fact]
    public void MalformedUuidAndOffsetlessInstant_AreRejectedByValueReaders()
    {
        using var uuidDocument = JsonDocument.Parse("""{"value":"not-a-uuid"}""");
        using var instantDocument = JsonDocument.Parse("""{"value":"2026-07-29T09:00:00"}""");

        Assert.Throws<FormatException>(
            () => VectorJson.ReadNullableGuid(uuidDocument.RootElement.GetProperty("value")));
        Assert.Throws<FormatException>(
            () => VectorJson.ReadInstant(instantDocument.RootElement.GetProperty("value")));
    }

    [Fact]
    public void SelectCurrentVectors_MatchCore()
    {
        var vectors = VectorFile.Load("select-current.json", "selectCurrent");

        foreach (var testCase in vectors.Cases)
        {
            var schedule = VectorJson.ReadSchedule(testCase.Input.GetProperty("schedule"));
            var now = VectorJson.ReadInstant(testCase.Input.GetProperty("now"));
            var actual = ScheduledTodoSelector.GetCurrent(schedule, now);
            var expected = VectorJson.ReadNullableGuid(
                testCase.Expected.GetProperty("currentTodoId"));

            Assert.Equal(expected, actual?.Id);
        }
    }

    [Fact]
    public void InsertScheduleVectors_MatchCoreAndStableOutputOrder()
    {
        var vectors = VectorFile.Load("insert-schedule.json", "insertSchedule");

        foreach (var testCase in vectors.Cases)
        {
            var input = VectorJson.ReadSchedule(testCase.Input.GetProperty("schedule"));
            var reversedInput = input.Reverse().ToArray();
            var proposed = VectorJson.ReadTodo(testCase.Input.GetProperty("proposedTodo"));
            var expectedSchedule = VectorJson.ReadSchedule(
                testCase.Expected.GetProperty("schedule"));

            var actual = ScheduleTrial.Insert(reversedInput, proposed);

            VectorAssert.EqualSchedule(expectedSchedule, actual.ScheduledTodos);
            Assert.Equal(
                testCase.Expected.GetProperty("hasRolloverToNextDay").GetBoolean(),
                actual.HasRolloverToNextDay);
            VectorAssert.EqualConflicts(
                testCase.Expected.GetProperty("conflicts"),
                actual.Conflicts);
            VectorAssert.IsStableSchedule(actual.ScheduledTodos);
        }
    }

    [Fact]
    public void ReminderVectors_MatchCore()
    {
        var vectors = VectorFile.Load("evaluate-reminder.json", "evaluateReminder");

        foreach (var testCase in vectors.Cases)
        {
            var schedule = VectorJson.ReadSchedule(testCase.Input.GetProperty("schedule"));
            var now = VectorJson.ReadInstant(testCase.Input.GetProperty("now"));

            var actual = HandoffReminderPolicy.Evaluate(schedule, now);

            Assert.Equal(
                testCase.Expected.GetProperty("shouldNotifyNow").GetBoolean(),
                actual.ShouldNotifyNow);
            Assert.Equal(
                testCase.Expected.GetProperty("ineligibility").GetString(),
                VectorJson.ToCamelCase(actual.Ineligibility.ToString()));
            Assert.Equal(
                VectorJson.ReadNullableGuid(testCase.Expected.GetProperty("currentTodoId")),
                actual.CurrentTodo?.Id);
            Assert.Equal(
                VectorJson.ReadNullableGuid(testCase.Expected.GetProperty("nextTodoId")),
                actual.NextTodo?.Id);
        }
    }

    [Fact]
    public void ManagementVectors_MatchCore()
    {
        var vectors = VectorFile.Load("manage-schedule.json", "manageSchedule");

        foreach (var testCase in vectors.Cases)
        {
            var input = testCase.Input;
            var schedule = VectorJson.ReadSchedule(input.GetProperty("schedule"));
            var todoId = Guid.Parse(input.GetProperty("todoId").GetString()!);
            var action = input.GetProperty("action").GetString();
            if (testCase.Expected.TryGetProperty("status", out var status)
                && status.GetString() == "rejected")
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => ExecuteRejectedManagementAction(action, input, schedule, todoId));
                var actualReason = exception.Message switch
                {
                    "Break events cannot be renamed." => "breakCannotBeRenamed",
                    "Mandatory todo occupies the requested start time." =>
                        "mandatoryTodoOccupiesNewStart",
                    _ => throw new VectorFormatException(
                        $"Unmapped management rejection: {exception.Message}")
                };
                Assert.Equal(
                    testCase.Expected.GetProperty("reason").GetString(),
                    actualReason);
                continue;
            }

            IReadOnlyList<ScheduledTodo> actual;

            if (action == "reorder")
            {
                var result = ScheduleManagement.Reorder(
                    schedule,
                    todoId,
                    input.GetProperty("requestedIndex").GetInt32());
                Assert.Equal(
                    testCase.Expected.GetProperty("actualIndex").GetInt32(),
                    result.ActualIndex);
                Assert.Equal(
                    testCase.Expected.GetProperty("usedFallbackPosition").GetBoolean(),
                    result.UsedFallbackPosition);
                actual = result.ScheduledTodos;
            }
            else if (action == "delete")
            {
                actual = ScheduleManagement.Delete(schedule, todoId);
            }
            else if (action == "insertBreak")
            {
                actual = ScheduleManagement.InsertBreak(
                    schedule,
                    todoId,
                    VectorJson.ReadInstant(input.GetProperty("start")),
                    TimeSpan.FromSeconds(input.GetProperty("durationSeconds").GetInt32()),
                    input.GetProperty("currentSelectionPriority").GetInt64()).ScheduledTodos;
            }
            else if (action == "edit")
            {
                actual = ScheduleManagement.Edit(
                    schedule,
                    todoId,
                    input.GetProperty("title").GetString()!,
                    TimeSpan.FromSeconds(input.GetProperty("durationSeconds").GetInt32()),
                    input.GetProperty("isMandatory").GetBoolean());
            }
            else if (action == "editStart")
            {
                var result = ScheduleManagement.EditStart(
                    schedule,
                    todoId,
                    VectorJson.ReadInstant(input.GetProperty("newStart")),
                    ReadStartConflictResolution(input.GetProperty("conflictResolution")));
                Assert.Equal(StartTimeEditRejection.None, result.Rejection);
                actual = result.ScheduledTodos;
            }
            else
            {
                throw new VectorFormatException($"Unsupported management action {action}.");
            }

            VectorAssert.EqualSchedule(
                VectorJson.ReadSchedule(testCase.Expected.GetProperty("schedule")),
                actual);
            VectorAssert.IsStableSchedule(actual);
        }
    }

    private static void ExecuteRejectedManagementAction(
        string? action,
        JsonElement input,
        IReadOnlyList<ScheduledTodo> schedule,
        Guid todoId)
    {
        if (action != "edit")
        {
            if (action == "editStart")
            {
                var result = ScheduleManagement.EditStart(
                    schedule,
                    todoId,
                    VectorJson.ReadInstant(input.GetProperty("newStart")),
                    ReadStartConflictResolution(input.GetProperty("conflictResolution")));
                if (result.Rejection == StartTimeEditRejection.MandatoryTodoOccupiesNewStart)
                {
                    throw new InvalidOperationException(
                        "Mandatory todo occupies the requested start time.");
                }

                throw new VectorFormatException(
                    $"Expected rejected start edit, found {result.Rejection}.");
            }

            throw new VectorFormatException($"Unsupported rejected management action {action}.");
        }

        ScheduleManagement.Edit(
            schedule,
            todoId,
            input.GetProperty("title").GetString()!,
            TimeSpan.FromSeconds(input.GetProperty("durationSeconds").GetInt32()),
            input.GetProperty("isMandatory").GetBoolean());
    }

    private static StartTimeConflictResolution? ReadStartConflictResolution(
        JsonElement element) =>
        element.ValueKind == JsonValueKind.Null
            ? null
            : element.GetString() switch
            {
                "moveExistingAfterEdited" =>
                    StartTimeConflictResolution.MoveExistingAfterEdited,
                "truncateExistingAtNewStart" =>
                    StartTimeConflictResolution.TruncateExistingAtNewStart,
                var value => throw new VectorFormatException(
                    $"Unsupported start conflict resolution {value}.")
            };

    [Fact]
    public void LocalDateTimeVectors_MatchApplicationBoundary()
    {
        var vectors = VectorFile.Load("resolve-local-date-time.json", "resolveLocalDateTime");

        foreach (var testCase in vectors.Cases)
        {
            var local = DateTime.ParseExact(
                testCase.Input.GetProperty("localDateTime").GetString()!,
                "yyyy-MM-dd'T'HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(
                testCase.Input.GetProperty("timeZoneId").GetString()!);
            var expectedStatus = testCase.Expected.GetProperty("status").GetString();

            if (expectedStatus == "resolved")
            {
                var actual = LocalDateTimeResolver.Resolve(local, zone);
                Assert.Equal(
                    VectorJson.ReadInstant(testCase.Expected.GetProperty("instant")),
                    actual);
                continue;
            }

            var exception = Assert.Throws<ArgumentException>(
                () => LocalDateTimeResolver.Resolve(local, zone));
            Assert.Contains(
                expectedStatus == "invalid" ? "does not exist" : "occurs twice",
                exception.Message);
        }
    }

    [Fact]
    public async Task ApplicationLifecycleVectors_MatchPlanningAndDeleteUseCases()
    {
        var vectors = VectorFile.Load("application-lifecycle.json", "applicationLifecycle");

        foreach (var testCase in vectors.Cases)
        {
            var action = testCase.Input.GetProperty("action").GetString();

            if (action == "planTwice")
            {
                var futureTodo = VectorJson.ReadFutureTodo(
                    testCase.Input.GetProperty("futureTodo"));
                var scheduledRepository = new VectorScheduledRepository();
                var futureRepository = new VectorFutureRepository(
                    [futureTodo],
                    testCase.Input.GetProperty("failFirstMarkPlanned").GetBoolean());
                var service = new TodoPlanningService(
                    scheduledRepository,
                    futureRepository,
                    TimeProvider.System);
                var request = new PlanUnscheduledTodoRequest(
                    futureTodo.Id,
                    TimeSpan.FromSeconds(
                        testCase.Input.GetProperty("durationSeconds").GetInt32()),
                    VectorJson.ReadInstant(testCase.Input.GetProperty("start")));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.PlanUnscheduledTodoAsync(request));
                await service.PlanUnscheduledTodoAsync(request);

                Assert.Equal(
                    Guid.Parse(testCase.Expected.GetProperty("scheduledTodoId").GetString()!),
                    Assert.Single(scheduledRepository.Items).Id);
                Assert.Equal(
                    testCase.Expected.GetProperty("replaceCount").GetInt32(),
                    scheduledRepository.ReplaceCount);
                Assert.Equal(
                    testCase.Expected.GetProperty("futureStatus").GetString(),
                    futureRepository.GetStatus(futureTodo.Id));
            }
            else if (action == "deleteFuture")
            {
                var futureTodo = VectorJson.ReadFutureTodo(
                    testCase.Input.GetProperty("futureTodo"));
                var scheduledRepository = new VectorScheduledRepository();
                var futureRepository = new VectorFutureRepository([futureTodo]);
                var service = new TodoPlanningService(
                    scheduledRepository,
                    futureRepository,
                    TimeProvider.System);
                await service.DeleteUnscheduledTodoAsync(
                    futureTodo.Id,
                    testCase.Input.GetProperty("isConfirmed").GetBoolean());
                Assert.Equal(
                    testCase.Expected.GetProperty("futureStatus").GetString(),
                    futureRepository.GetStatus(futureTodo.Id));
            }
            else if (action == "loadManagement")
            {
                var now = VectorJson.ReadInstant(testCase.Input.GetProperty("now"));
                var scheduledRepository = new VectorScheduledRepository(
                    VectorJson.ReadSchedule(testCase.Input.GetProperty("scheduledTodos")));
                var service = new ScheduleManagementService(
                    scheduledRepository,
                    new VectorFutureRepository([]),
                    new FixedVectorTimeProvider(now));

                var state = await service.LoadAsync();

                Assert.Equal(
                    VectorJson.ReadGuidArray(testCase.Expected.GetProperty("activeTodoIds")),
                    state.ScheduledTodos.Select(todo => todo.Id));
                VectorAssert.EqualHistory(
                    testCase.Expected.GetProperty("history"),
                    scheduledRepository.History);
            }
            else if (action == "loadOpening")
            {
                var now = VectorJson.ReadInstant(testCase.Input.GetProperty("now"));
                var futureTodos = testCase.Input.GetProperty("futureTodos")
                    .EnumerateArray()
                    .Select(VectorJson.ReadFutureTodo)
                    .ToArray();
                var service = new TodoPlanningService(
                    new VectorScheduledRepository(),
                    new VectorFutureRepository(futureTodos),
                    new FixedVectorTimeProvider(now));

                var state = await service.LoadOpeningStateAsync();

                Assert.Equal(
                    VectorJson.ReadGuidArray(
                        testCase.Expected.GetProperty("dueFutureTodoIds")),
                    state.DueUnscheduledTodos.Select(todo => todo.Id));
            }
            else if (action == "deleteScheduled")
            {
                var now = VectorJson.ReadInstant(testCase.Input.GetProperty("now"));
                var scheduledRepository = new VectorScheduledRepository(
                    VectorJson.ReadSchedule(testCase.Input.GetProperty("scheduledTodos")));
                var service = new ScheduleManagementService(
                    scheduledRepository,
                    new VectorFutureRepository([]),
                    new FixedVectorTimeProvider(now));

                var active = await service.DeleteAsync(
                    Guid.Parse(testCase.Input.GetProperty("todoId").GetString()!),
                    testCase.Input.GetProperty("isConfirmed").GetBoolean());

                Assert.Equal(
                    VectorJson.ReadGuidArray(testCase.Expected.GetProperty("activeTodoIds")),
                    active.Select(todo => todo.Id));
                VectorAssert.EqualHistory(
                    testCase.Expected.GetProperty("history"),
                    scheduledRepository.History);
            }
            else if (action == "replaceSchedule")
            {
                var now = VectorJson.ReadInstant(testCase.Input.GetProperty("now"));
                var scheduledRepository = new VectorScheduledRepository(
                    VectorJson.ReadSchedule(testCase.Input.GetProperty("scheduledTodos")));
                await scheduledRepository.MarkCompletedBeforeAsync(now);
                await scheduledRepository.ReplaceAllAsync(
                    VectorJson.ReadSchedule(
                        testCase.Input.GetProperty("replacementSchedule")));

                VectorAssert.EqualHistory(
                    testCase.Expected.GetProperty("history"),
                    scheduledRepository.History);
            }
            else if (action == "addRelativeFuture")
            {
                var now = VectorJson.ReadInstant(testCase.Input.GetProperty("now"));
                var future = VectorJson.ReadFutureTodo(
                    testCase.Input.GetProperty("futureTodo"));
                var futureRepository = new VectorFutureRepository([]);
                var service = new TodoPlanningService(
                    new VectorScheduledRepository(),
                    futureRepository,
                    new FixedVectorTimeProvider(now));

                var saved = await service.AddUnscheduledTodoAsync(
                    new AddUnscheduledTodoRequest(
                        future.Id,
                        future.Title,
                        DaysFromToday:
                            testCase.Input.GetProperty("daysFromToday").GetInt32(),
                        IsMandatory: future.IsMandatory));

                Assert.Equal(
                    DateOnly.ParseExact(
                        testCase.Expected.GetProperty("savedDate").GetString()!,
                        "yyyy-MM-dd"),
                    saved.ScheduledDate);
                Assert.Equal(saved, futureRepository.SavedTodo);
            }
            else
            {
                throw new VectorFormatException($"Unsupported lifecycle action {action}.");
            }
        }
    }
}

internal static class VectorPaths
{
    public static string VersionOneDirectory =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "test-vectors",
                "v1"));
}

internal sealed record VectorCase(
    string Id,
    JsonElement Input,
    JsonElement Expected);

internal sealed record VectorFile(
    int SchemaVersion,
    string Operation,
    IReadOnlyList<VectorCase> Cases)
{
    private static readonly HashSet<string> SupportedOperations =
    [
        "selectCurrent",
        "insertSchedule",
        "manageSchedule",
        "evaluateReminder",
        "resolveLocalDateTime",
        "applicationLifecycle"
    ];

    public static VectorFile Load(string fileName, string expectedOperation)
    {
        var path = Path.Combine(VectorPaths.VersionOneDirectory, fileName);
        var file = Parse(File.ReadAllText(path));
        if (file.Operation != expectedOperation)
        {
            throw new VectorFormatException(
                $"Expected operation {expectedOperation}, found {file.Operation}.");
        }

        return file;
    }

    public static VectorFile Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = root.GetProperty("schemaVersion").GetInt32();
            if (version != 1)
            {
                throw new VectorFormatException($"Unsupported schemaVersion {version}.");
            }

            var operation = root.GetProperty("operation").GetString();
            if (operation is null || !SupportedOperations.Contains(operation))
            {
                throw new VectorFormatException($"Unsupported operation {operation ?? "<null>"}.");
            }

            var cases = root.GetProperty("cases")
                .EnumerateArray()
                .Select(element => new VectorCase(
                    element.GetProperty("id").GetString()
                        ?? throw new VectorFormatException("Case id cannot be null."),
                    element.GetProperty("input").Clone(),
                    element.GetProperty("expected").Clone()))
                .ToArray();
            return new(version, operation, cases);
        }
        catch (VectorFormatException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            throw new VectorFormatException("Malformed vector envelope.", exception);
        }
    }
}

internal sealed class VectorFormatException : Exception
{
    public VectorFormatException(string message)
        : base(message)
    {
    }

    public VectorFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal static class VectorJson
{
    public static IReadOnlyList<ScheduledTodo> ReadSchedule(JsonElement element) =>
        element.EnumerateArray().Select(ReadTodo).ToArray();

    public static ScheduledTodo ReadTodo(JsonElement element) =>
        new(
            Guid.ParseExact(element.GetProperty("id").GetString()!, "D"),
            element.GetProperty("title").GetString()!,
            new TimeRange(
                ReadInstant(element.GetProperty("start")),
                ReadInstant(element.GetProperty("end"))),
            element.GetProperty("isMandatory").GetBoolean(),
            element.GetProperty("currentSelectionPriority").GetInt64());

    public static DateTimeOffset ReadInstant(JsonElement element) =>
        DateTimeOffset.ParseExact(
            element.GetString()!,
            "yyyy-MM-dd'T'HH:mm:sszzz",
            System.Globalization.CultureInfo.InvariantCulture);

    public static Guid? ReadNullableGuid(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null
            ? null
            : Guid.ParseExact(element.GetString()!, "D");

    public static UnscheduledTodo ReadFutureTodo(JsonElement element) =>
        new(
            Guid.ParseExact(element.GetProperty("id").GetString()!, "D"),
            element.GetProperty("title").GetString()!,
            DateOnly.ParseExact(
                element.GetProperty("scheduledDate").GetString()!,
                "yyyy-MM-dd"),
            element.GetProperty("isMandatory").GetBoolean());

    public static IReadOnlyList<Guid> ReadGuidArray(JsonElement element) =>
        element.EnumerateArray()
            .Select(value => Guid.ParseExact(value.GetString()!, "D"))
            .ToArray();

    public static string ToCamelCase(string value) =>
        char.ToLowerInvariant(value[0]) + value[1..];
}

internal static class VectorAssert
{
    public static void EqualSchedule(
        IReadOnlyList<ScheduledTodo> expected,
        IReadOnlyList<ScheduledTodo> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].Id, actual[index].Id);
            Assert.Equal(expected[index].Title, actual[index].Title);
            Assert.Equal(expected[index].TimeRange, actual[index].TimeRange);
            Assert.Equal(expected[index].IsMandatory, actual[index].IsMandatory);
            Assert.Equal(
                expected[index].CurrentSelectionPriority,
                actual[index].CurrentSelectionPriority);
        }
    }

    public static void EqualConflicts(
        JsonElement expected,
        IReadOnlyList<ScheduleConflict> actual)
    {
        var expectedPairs = expected.EnumerateArray()
            .Select(element => (
                Proposed: Guid.Parse(element.GetProperty("proposedTodoId").GetString()!),
                Mandatory: Guid.Parse(element.GetProperty("mandatoryTodoId").GetString()!)))
            .ToArray();
        var actualPairs = actual
            .Select(conflict => (conflict.ProposedTodo.Id, conflict.MandatoryTodo.Id))
            .ToArray();

        Assert.Equal(expectedPairs, actualPairs);
    }

    public static void IsStableSchedule(IReadOnlyList<ScheduledTodo> schedule)
    {
        var stable = schedule
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .Select(todo => todo.Id);

        Assert.Equal(stable, schedule.Select(todo => todo.Id));
    }

    public static void EqualHistory(
        JsonElement expected,
        IReadOnlyDictionary<Guid, VectorScheduledHistory> actual)
    {
        var expectedItems = expected.EnumerateArray().ToArray();
        Assert.Equal(expectedItems.Length, actual.Count);

        foreach (var expectedItem in expectedItems)
        {
            var id = Guid.Parse(expectedItem.GetProperty("id").GetString()!);
            var actualItem = actual[id];
            Assert.Equal(expectedItem.GetProperty("status").GetString(), actualItem.Status);
            var completedAt = expectedItem.GetProperty("completedAt");
            Assert.Equal(
                completedAt.ValueKind == JsonValueKind.Null
                    ? null
                    : VectorJson.ReadInstant(completedAt),
                actualItem.CompletedAt);
        }
    }
}

internal sealed record VectorScheduledHistory(
    ScheduledTodo Todo,
    string Status,
    DateTimeOffset? CompletedAt);

internal sealed class VectorScheduledRepository : IScheduledTodoRepository
{
    public VectorScheduledRepository(IReadOnlyList<ScheduledTodo>? items = null)
    {
        Items = items?.ToArray() ?? [];
        History = Items.ToDictionary(
            todo => todo.Id,
            todo => new VectorScheduledHistory(todo, "active", null));
    }

    public IReadOnlyList<ScheduledTodo> Items { get; private set; }

    public int ReplaceCount { get; private set; }

    public Dictionary<Guid, VectorScheduledHistory> History { get; }

    public Task<IReadOnlyList<ScheduledTodo>> LoadAllAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScheduledTodo>>(
            Items.Where(todo => History[todo.Id].Status == "active").ToArray());

    public Task ReplaceAllAsync(
        IReadOnlyList<ScheduledTodo> scheduledTodos,
        CancellationToken cancellationToken = default)
    {
        var replacementIds = scheduledTodos.Select(todo => todo.Id).ToHashSet();
        foreach (var existing in History.Values.Where(
                     entry => entry.Status == "active"
                              && !replacementIds.Contains(entry.Todo.Id)).ToArray())
        {
            History[existing.Todo.Id] = existing with { Status = "deleted" };
        }

        Items = scheduledTodos.ToArray();
        foreach (var todo in Items)
        {
            History[todo.Id] = new(todo, "active", null);
        }

        ReplaceCount++;
        return Task.CompletedTask;
    }

    public Task MarkCompletedBeforeAsync(
        DateTimeOffset completedBefore,
        CancellationToken cancellationToken = default)
    {
        foreach (var todo in Items.Where(todo => todo.TimeRange.End <= completedBefore))
        {
            History[todo.Id] = new(todo, "completed", completedBefore);
        }

        return Task.CompletedTask;
    }
}

internal sealed class VectorFutureRepository(
    IReadOnlyList<UnscheduledTodo> items,
    bool failFirstMarkPlanned = false) : IUnscheduledTodoRepository
{
    private readonly List<UnscheduledTodo> storedItems = items.ToList();
    private readonly Dictionary<Guid, string> statuses =
        items.ToDictionary(item => item.Id, _ => "active");

    public UnscheduledTodo? SavedTodo { get; private set; }

    public string GetStatus(Guid id) => statuses[id];

    public Task<IReadOnlyList<UnscheduledTodo>> LoadAllActiveAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UnscheduledTodo>>(
            storedItems.Where(item => statuses[item.Id] == "active").ToArray());

    public Task<IReadOnlyList<UnscheduledTodo>> LoadByDateAsync(
        DateOnly scheduledDate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UnscheduledTodo>>(
            storedItems.Where(item =>
                statuses[item.Id] == "active"
                && item.ScheduledDate == scheduledDate).ToArray());

    public Task<IReadOnlyList<UnscheduledTodo>> LoadDueOnOrBeforeAsync(
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UnscheduledTodo>>(
            storedItems.Where(item =>
                    statuses[item.Id] == "active"
                    && item.ScheduledDate <= date)
                .OrderBy(item => item.ScheduledDate)
                .ThenBy(item => item.Id)
                .ToArray());

    public Task<UnscheduledTodo?> LoadActiveByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            storedItems.SingleOrDefault(item =>
                item.Id == id
                && statuses[item.Id] == "active"));

    public Task SaveAsync(
        UnscheduledTodo todo,
        CancellationToken cancellationToken = default)
    {
        storedItems.Add(todo);
        statuses[todo.Id] = "active";
        SavedTodo = todo;
        return Task.CompletedTask;
    }

    public Task UpdateActiveAsync(
        UnscheduledTodo todo,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task MarkPlannedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Assert.Contains(id, statuses.Keys);
        if (failFirstMarkPlanned)
        {
            failFirstMarkPlanned = false;
            throw new InvalidOperationException("Injected first mark-planned failure.");
        }

        statuses[id] = "planned";
        return Task.CompletedTask;
    }

    public Task MarkDeletedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Assert.Contains(id, statuses.Keys);
        statuses[id] = "deleted";
        return Task.CompletedTask;
    }
}

internal sealed class FixedVectorTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone =>
        TimeZoneInfo.CreateCustomTimeZone(
            "VectorOffset",
            now.Offset,
            "VectorOffset",
            "VectorOffset");
}
