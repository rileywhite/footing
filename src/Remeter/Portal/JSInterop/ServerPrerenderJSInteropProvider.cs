using System;
using System.IO;
using System.Threading.Tasks;

namespace Remeter.Portal.JSInterop
{
    public class ServerPrerenderJSInteropProvider : IJSInterop
    {
        public Task<bool> Confirm(string message) => throw new NotSupportedException("JS calls not allowed during prerendering");

        public Task DownloadAs(MemoryStream stream, string defaultFileName, string mimeType) => throw new NotSupportedException("JS calls not allowed during prerendering");

        public Task InitializePopovers() => throw new NotSupportedException("JS calls not allowed during prerendering");
    }
}
