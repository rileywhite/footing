using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Remeter.Portal.Components
{
    public static class Extensions
    {
        public static decimal AsWeekly(this Period source, decimal amount) => source switch
        {
            Period.Weekly => amount,
            Period.BiWeekly => amount / 2m,
            Period.Monthly => amount * 12m / 52m,
            Period.SemiMonthly => amount * 24m / 52m,
            var unsupported => throw new NotSupportedException($"Unknown Period: {unsupported}"),
        };

        public static async Task InitializePopovers(this IJSRuntime jsRuntime)
        {
            await jsRuntime.InvokeVoidAsync("initializePopovers");
        }
    }
}
