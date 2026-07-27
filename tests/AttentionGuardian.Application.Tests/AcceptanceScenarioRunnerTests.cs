using AttentionGuardian.Application;

namespace AttentionGuardian.Application.Tests;

public sealed class AcceptanceScenarioRunnerTests
{
    [Fact]
    public void RunAll_CoversSpecialSchedulingAndReminderCases()
    {
        var results = AcceptanceScenarioRunner.RunAll();

        Assert.Equal(9, results.Count);
        Assert.All(results, result => Assert.True(result.Passed, result.Actual));
        Assert.Contains(results, result => result.Name == "不可移动事件冲突");
        Assert.Contains(results, result => result.Name == "跨日顺延");
        Assert.Contains(results, result => result.Name == "跨零点当前事项");
        Assert.Contains(results, result => result.Name == "五分钟前 Windows 提醒资格");
    }
}
