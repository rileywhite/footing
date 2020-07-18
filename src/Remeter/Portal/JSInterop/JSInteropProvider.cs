using Microsoft.JSInterop;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Remeter.Portal.JSInterop
{
    public class JSInteropProvider : IJSInterop
    {
        private IJSRuntime JSRuntime { get; }

        public JSInteropProvider(IJSRuntime jsRuntime)
        {
            this.JSRuntime = jsRuntime;
        }

        public async Task InitializePopovers() => await this.JSRuntime.InvokeVoidAsync("initializePopovers");

        public async Task<bool> Confirm(string message) => await this.JSRuntime.InvokeAsync<bool>("confirm", message);

        public async Task DownloadAs(MemoryStream stream, string defaultFileName, string mimeType) =>
            await this.JSRuntime.InvokeAsync<object>(
                "saveAsFile",
                defaultFileName,
                mimeType,
                Convert.ToBase64String(stream.ToArray()));
    }
}
