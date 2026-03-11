namespace Footing.Models;

public static class Extensions
{
    public static decimal AsWeekly(this Period source, decimal amount) => source switch
    {
        Period.Daily => amount * 7m,
        Period.Weekly => amount,
        Period.BiWeekly => amount / 2m,
        Period.SemiMonthly => amount * 24m / 52m,
        Period.Monthly => amount * 12m / 52m,
        Period.Quarterly => amount * 4m / 52m,
        Period.SemiAnnually => amount * 2m / 52m,
        Period.Annually => amount / 52m,
        var unsupported => throw new NotSupportedException($"Unknown Period: {unsupported}"),
    };

    public static int PeriodsPerYear(this Period source) => source switch
    {
        Period.Daily => 365,
        Period.Weekly => 52,
        Period.BiWeekly => 26,
        Period.SemiMonthly => 24,
        Period.Monthly => 12,
        Period.Quarterly => 4,
        Period.SemiAnnually => 2,
        Period.Annually => 1,
        var unsupported => throw new NotSupportedException($"Unknown Period: {unsupported}"),
    };
}
