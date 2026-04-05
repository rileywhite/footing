using FluentAssertions;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Unit.Models;

public class PeriodExtensionsTests
{
    [Fact]
    public void AsWeekly_Daily_MultipliesBy7() =>
        Period.Daily.AsWeekly(10m).Should().Be(70m);

    [Fact]
    public void AsWeekly_Weekly_ReturnsUnchanged() =>
        Period.Weekly.AsWeekly(100m).Should().Be(100m);

    [Fact]
    public void AsWeekly_BiWeekly_DividesByTwo() =>
        Period.BiWeekly.AsWeekly(200m).Should().Be(100m);

    [Fact]
    public void AsWeekly_SemiMonthly_ConvertsCorrectly() =>
        Period.SemiMonthly.AsWeekly(100m).Should().BeApproximately(100m * 24m / 52m, 0.0001m);

    [Fact]
    public void AsWeekly_Monthly_ConvertsCorrectly() =>
        Period.Monthly.AsWeekly(520m).Should().Be(520m * 12m / 52m);

    [Fact]
    public void AsWeekly_Quarterly_ConvertsCorrectly() =>
        Period.Quarterly.AsWeekly(1300m).Should().Be(1300m * 4m / 52m);

    [Fact]
    public void AsWeekly_SemiAnnually_ConvertsCorrectly() =>
        Period.SemiAnnually.AsWeekly(2600m).Should().Be(2600m * 2m / 52m);

    [Fact]
    public void AsWeekly_Annually_DividesByFiftyTwo() =>
        Period.Annually.AsWeekly(5200m).Should().Be(100m);

    [Fact]
    public void AsWeekly_ZeroAmount_ReturnsZero() =>
        Period.Monthly.AsWeekly(0m).Should().Be(0m);

    [Theory]
    [InlineData(Period.Daily, 365)]
    [InlineData(Period.Weekly, 52)]
    [InlineData(Period.BiWeekly, 26)]
    [InlineData(Period.SemiMonthly, 24)]
    [InlineData(Period.Monthly, 12)]
    [InlineData(Period.Quarterly, 4)]
    [InlineData(Period.SemiAnnually, 2)]
    [InlineData(Period.Annually, 1)]
    public void PeriodsPerYear_ReturnsCorrectValue(Period period, int expected) =>
        period.PeriodsPerYear().Should().Be(expected);

    [Fact]
    public void AsWeekly_AllPeriods_ProduceConsistentAnnualTotal()
    {
        var annualAmount = 5200m;
        Period.Weekly.AsWeekly(annualAmount / 52m).Should().Be(100m);
        Period.Annually.AsWeekly(annualAmount).Should().Be(100m);
        Period.Monthly.AsWeekly(annualAmount / 12m).Should().Be(100m);
        Period.Quarterly.AsWeekly(annualAmount / 4m).Should().Be(100m);
    }
}
