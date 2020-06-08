using System;

namespace Remeter.Portal.Components
{
    public static class Extensions
    {
        public static decimal AsWeekly(this MoneyFlows.Period source, decimal amount) => source switch
        {
            MoneyFlows.Period.Weekly => amount,
            MoneyFlows.Period.BiWeekly => amount / 2m,
            MoneyFlows.Period.Monthly => amount * 12m / 52m,
            MoneyFlows.Period.SemiMonthly => amount * 24m / 52m,
            var unsupported => throw new NotSupportedException($"Unknown Period: {unsupported}"),
        };
    }
}
