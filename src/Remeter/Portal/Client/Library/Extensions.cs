using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace Remeter.Portal.Client.Library
{
    public static class Extensions
    {
        public static async Task InitializePopovers(this IJSRuntime jsRuntime)
        {
            await jsRuntime.InvokeVoidAsync("initializePopovers");
        }
    }
}
