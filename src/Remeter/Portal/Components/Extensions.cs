using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

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

        public static async Task InitializePopovers(this IJSRuntime jsRuntime)
        {
            await jsRuntime.InvokeVoidAsync("initializePopovers");
        }
    }
}
