namespace Footing.Models;

public static class Extensions
{
    public static decimal AsWeekly(this Period source, decimal amount) =>
        amount * Year.DaysPerWeek / source.DaysPerPeriod();

    public static decimal PeriodsPerYear(this Period source) =>
        Year.Days / source.DaysPerPeriod();

    // Every value here is a terminating decimal, so conversions through it stay exact.
    public static decimal DaysPerPeriod(this Period source) => source switch
    {
        Period.Daily => 1m,
        Period.Weekly => Year.DaysPerWeek,
        Period.BiWeekly => 2m * Year.DaysPerWeek,
        Period.SemiMonthly => Year.Days / 24m,
        Period.Monthly => Year.Days / 12m,
        Period.Quarterly => Year.Days / 4m,
        Period.SemiAnnually => Year.Days / 2m,
        Period.Annually => Year.Days,
        var unsupported => throw new NotSupportedException($"Unknown Period: {unsupported}"),
    };
}
