using System;
using System.IO;
using System.Threading.Tasks;

namespace Remeter.Portal.JSInterop
{
    public interface IJSInterop
    {
        Task InitializePopovers();
        Task<bool> Confirm(string message);
        Task DownloadAs(MemoryStream stream, string defaultFileName, string mimeType);
    }
}
