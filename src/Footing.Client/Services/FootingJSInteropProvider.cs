using Microsoft.JSInterop;

namespace Footing.Client.Services;

public class FootingJSInteropProvider(IJSRuntime jsRuntime) : IFootingJSInterop
{
    public async Task InitializePopovers() =>
        await jsRuntime.InvokeVoidAsync("Footing.initializePopovers");

    public async Task<bool> Confirm(string message) =>
        await jsRuntime.InvokeAsync<bool>("confirm", message);

    public async Task DownloadAs(MemoryStream stream, string defaultFileName, string mimeType) =>
        await jsRuntime.InvokeVoidAsync(
            "Footing.saveAsFile",
            defaultFileName,
            mimeType,
            Convert.ToBase64String(stream.ToArray()));
}
