using FluentAssertions;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Unit.Models;

public class PeriodExtensionsTests
{
    [Fact]
    public void Year_IsTheAverageGregorianYear()
    {
        Year.Days.Should().Be(365.2425m);
        Year.Days.Should().Be(365m + 97m / 400m);
        Year.DaysPerWeek.Should().Be(7m);
        Year.Weeks.Should().Be(52.1775m);
    }

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
        Period.SemiMonthly.AsWeekly(2174.0625m).Should().Be(1000m);

    [Fact]
    public void AsWeekly_Monthly_ConvertsCorrectly() =>
        Period.Monthly.AsWeekly(4348.125m).Should().Be(1000m);

    [Fact]
    public void AsWeekly_Quarterly_ConvertsCorrectly() =>
        Period.Quarterly.AsWeekly(13044.375m).Should().Be(1000m);

    [Fact]
    public void AsWeekly_SemiAnnually_ConvertsCorrectly() =>
        Period.SemiAnnually.AsWeekly(26088.75m).Should().Be(1000m);

    [Fact]
    public void AsWeekly_Annually_DividesByWeeksPerYear() =>
        Period.Annually.AsWeekly(52177.5m).Should().Be(1000m);

    [Fact]
    public void AsWeekly_Monthly1000_RoundsTo229_98() =>
        ((MonetaryAmount)Period.Monthly.AsWeekly(1000m)).RoundedAmount.Amount.Should().Be(229.98m);

    [Fact]
    public void AsWeekly_Annually60000_RoundsTo1149_92() =>
        ((MonetaryAmount)Period.Annually.AsWeekly(60000m)).RoundedAmount.Amount.Should().Be(1149.92m);

    [Fact]
    public void AsWeekly_ZeroAmount_ReturnsZero() =>
        Period.Monthly.AsWeekly(0m).Should().Be(0m);

    public static TheoryData<Period, decimal> ExpectedDaysPerPeriod => new()
    {
        { Period.Daily, 1m },
        { Period.Weekly, 7m },
        { Period.BiWeekly, 14m },
        { Period.SemiMonthly, 15.2184375m },
        { Period.Monthly, 30.436875m },
        { Period.Quarterly, 91.310625m },
        { Period.SemiAnnually, 182.62125m },
        { Period.Annually, 365.2425m },
    };

    [Theory]
    [MemberData(nameof(ExpectedDaysPerPeriod))]
    public void DaysPerPeriod_ReturnsCorrectValue(Period period, decimal expected) =>
        period.DaysPerPeriod().Should().Be(expected);

    public static TheoryData<Period, decimal> ExpectedPeriodsPerYear => new()
    {
        { Period.Daily, 365.2425m },
        { Period.Weekly, 52.1775m },
        { Period.BiWeekly, 26.08875m },
        { Period.SemiMonthly, 24m },
        { Period.Monthly, 12m },
        { Period.Quarterly, 4m },
        { Period.SemiAnnually, 2m },
        { Period.Annually, 1m },
    };

    [Theory]
    [MemberData(nameof(ExpectedPeriodsPerYear))]
    public void PeriodsPerYear_ReturnsCorrectValue(Period period, decimal expected) =>
        period.PeriodsPerYear().Should().Be(expected);

    [Fact]
    public void AsWeekly_AllPeriods_ProduceConsistentAnnualTotal()
    {
        var annualAmount = 365242.5m;
        foreach (var period in Enum.GetValues<Period>())
        {
            period.AsWeekly(annualAmount / period.PeriodsPerYear())
                .Should().Be(7000m, "a year of {0} amounts totals {1}", period, annualAmount);
        }
    }

    [Fact]
    public void AsWeekly_UnknownPeriod_Throws() =>
        FluentActions.Invoking(() => ((Period)999).AsWeekly(1m))
            .Should().Throw<NotSupportedException>();
}
