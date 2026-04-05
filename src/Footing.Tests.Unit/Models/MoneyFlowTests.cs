using FluentAssertions;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Unit.Models;

public class MoneyFlowTests
{
    [Fact]
    public void NewMoneyFlow_HasUniqueId()
    {
        var a = new MoneyFlow();
        var b = new MoneyFlow();
        a.Id.Should().NotBe(b.Id);
        a.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void GetWeeklyAmount_WeeklyPeriod_ReturnsSameAmount() =>
        TestDataGenerator.CreateMoneyFlow(amount: 200m, period: Period.Weekly)
            .GetWeeklyAmount().Should().Be(200m);

    [Fact]
    public void GetWeeklyAmount_MonthlyPeriod_ConvertsCorrectly() =>
        TestDataGenerator.CreateMoneyFlow(amount: 520m, period: Period.Monthly)
            .GetWeeklyAmount().Should().Be(520m * 12m / 52m);

    [Fact]
    public void GetWeeklyAmount_AnnualPeriod_DividesByFiftyTwo() =>
        TestDataGenerator.CreateMoneyFlow(amount: 5200m, period: Period.Annually)
            .GetWeeklyAmount().Should().Be(100m);

    [Fact]
    public void GetWeeklyAmount_DailyPeriod_MultipliesBySeven() =>
        TestDataGenerator.CreateMoneyFlow(amount: 15m, period: Period.Daily)
            .GetWeeklyAmount().Should().Be(105m);

    [Fact]
    public void GetWeeklyAmount_ZeroAmount_ReturnsZero() =>
        TestDataGenerator.CreateMoneyFlow(amount: 0m, period: Period.Monthly)
            .GetWeeklyAmount().Should().Be(0m);
}
