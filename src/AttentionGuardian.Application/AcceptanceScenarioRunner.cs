using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed record AcceptanceScenarioResult(
    string Name,
    string Expected,
    string Actual,
    bool Passed);

public static class AcceptanceScenarioRunner
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 7, 27, 20, 0, 0, TimeSpan.FromHours(8));

    public static IReadOnlyList<AcceptanceScenarioResult> RunAll()
    {
        var results = new List<AcceptanceScenarioResult>();
        Run(results, "普通级联顺延", "两项普通事件连续后移", () =>
        {
            var first = Todo("A", 0, 30);
            var second = Todo("B", 30, 60);
            var inserted = Todo("新任务", 0, 45);
            var plan = ScheduleTrial.Insert([first, second], inserted);
            return plan.ScheduledTodos.Single(todo => todo.Id == second.Id).TimeRange.Start
                == BaseTime.AddMinutes(75);
        });
        Run(results, "越过多个不可移动事件", "普通事件保持时长并落在最后一个挡板之后", () =>
        {
            var first = Todo("不可移动 A", 15, 45, mandatory: true);
            var second = Todo("不可移动 B", 45, 75, mandatory: true);
            var inserted = Todo("普通", 0, 30);
            var plan = ScheduleTrial.Insert([first, second], inserted);
            var actual = plan.ScheduledTodos.Single(todo => todo.Id == inserted.Id);
            return actual.TimeRange.Start == BaseTime.AddMinutes(75)
                && actual.TimeRange.Duration == TimeSpan.FromMinutes(30);
        });
        Run(results, "不可移动事件冲突", "保存完整计划并返回待管理冲突", () =>
        {
            var existing = Todo("不可移动 A", 0, 60, mandatory: true);
            var inserted = Todo("不可移动 B", 30, 90, mandatory: true);
            var plan = ScheduleTrial.Insert([existing], inserted);
            return plan.Conflicts.Count == 1 && plan.ScheduledTodos.Count == 2;
        });
        Run(results, "跨日顺延", "计划进入次日并设置跨日标识", () =>
        {
            var lateBase = new DateTimeOffset(2026, 7, 27, 23, 40, 0, TimeSpan.FromHours(8));
            var existing = TodoAt(lateBase, "A", 0, 30);
            var inserted = TodoAt(lateBase, "新任务", 0, 40);
            var plan = ScheduleTrial.Insert([existing], inserted);
            return plan.HasRolloverToNextDay;
        });
        Run(results, "空档停止顺延", "空档后的事件保持原时刻", () =>
        {
            var first = Todo("A", 0, 20);
            var afterGap = Todo("B", 60, 90);
            var inserted = Todo("新任务", 0, 30);
            var plan = ScheduleTrial.Insert([first, afterGap], inserted);
            return plan.ScheduledTodos.Single(todo => todo.Id == afterGap.Id).TimeRange.Start
                == afterGap.TimeRange.Start;
        });
        Run(results, "首尾相接边界", "前一事项结束时切换到后一事项", () =>
        {
            var first = Todo("A", 0, 30);
            var second = Todo("B", 30, 60);
            return ScheduledTodoSelector.GetCurrent([first, second], BaseTime.AddMinutes(30))?.Id
                == second.Id;
        });
        Run(results, "跨零点当前事项", "零点后仍能匹配跨日事项", () =>
        {
            var start = new DateTimeOffset(2026, 7, 27, 23, 50, 0, TimeSpan.FromHours(8));
            var todo = TodoAt(start, "跨日", 0, 30);
            return ScheduledTodoSelector.GetCurrent([todo], start.AddMinutes(20))?.Id == todo.Id;
        });
        Run(results, "五分钟前 Windows 提醒资格", "无空档相接的非休息事项可提醒", () =>
        {
            var current = Todo("当前", 0, 30);
            var next = Todo("下一项", 30, 60);
            return HandoffReminderPolicy.Evaluate(
                [current, next],
                BaseTime.AddMinutes(25)).ShouldNotifyNow;
        });
        Run(results, "休息与空档不提醒", "进入休息或存在空档时均不提醒", () =>
        {
            var current = Todo("当前", 0, 30);
            var rest = Todo(ScheduleManagement.BreakTitle, 30, 50);
            var afterGap = Todo("稍后", 40, 70);
            return !HandoffReminderPolicy.Evaluate(
                    [current, rest],
                    BaseTime.AddMinutes(25)).ShouldNotifyNow
                && !HandoffReminderPolicy.Evaluate(
                    [current, afterGap],
                    BaseTime.AddMinutes(25)).ShouldNotifyNow;
        });
        return results;
    }

    private static ScheduledTodo Todo(
        string title,
        int startMinute,
        int endMinute,
        bool mandatory = false) =>
        TodoAt(BaseTime, title, startMinute, endMinute, mandatory);

    private static ScheduledTodo TodoAt(
        DateTimeOffset anchor,
        string title,
        int startMinute,
        int endMinute,
        bool mandatory = false) =>
        new(
            Guid.NewGuid(),
            title,
            new TimeRange(
                anchor.AddMinutes(startMinute),
                anchor.AddMinutes(endMinute)),
            mandatory);

    private static void Run(
        ICollection<AcceptanceScenarioResult> results,
        string name,
        string expected,
        Func<bool> check)
    {
        try
        {
            var passed = check();
            results.Add(new(
                name,
                expected,
                passed ? "符合预期" : "结果不符合预期",
                passed));
        }
        catch (Exception exception)
        {
            results.Add(new(name, expected, exception.Message, false));
        }
    }
}
