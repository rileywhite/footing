using FluentAssertions;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Unit.Models;

public class MoneyFlowsTests
{
    [Fact]
    public void WeeklyTotalMoneyFlow_Empty_ReturnsZero() =>
        new MoneyFlows { Direction = MoneyFlowDirection.Income }
            .WeeklyTotalMoneyFlow.Should().Be(0m);

    [Fact]
    public void WeeklyTotalMoneyFlow_SingleItem_ReturnsWeeklyAmount() =>
        TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Income,
                ("Salary", 5200m, Period.Annually))
            .WeeklyTotalMoneyFlow.Should().Be(100m);

    [Fact]
    public void WeeklyTotalMoneyFlow_MultipleItems_SumsCorrectly() =>
        TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Income,
                ("Salary", 100m, Period.Weekly),
                ("Freelance", 200m, Period.Weekly))
            .WeeklyTotalMoneyFlow.Should().Be(300m);

    [Fact]
    public void WeeklyTotalMoneyFlow_MixedPeriods_SumsConvertedAmounts()
    {
        var flows = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Outgo,
            ("Weekly Bill", 50m, Period.Weekly),
            ("Annual Bill", 5200m, Period.Annually));
        flows.WeeklyTotalMoneyFlow.Should().Be(150m);
    }

    [Fact]
    public void Direction_CanBeSetToIncome() =>
        new MoneyFlows { Direction = MoneyFlowDirection.Income }
            .Direction.Should().Be(MoneyFlowDirection.Income);

    [Fact]
    public void Direction_CanBeSetToOutgo() =>
        new MoneyFlows { Direction = MoneyFlowDirection.Outgo }
            .Direction.Should().Be(MoneyFlowDirection.Outgo);
}
