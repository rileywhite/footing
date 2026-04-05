namespace Footing.Client.Services;

public interface IFootingJSInterop
{
    Task<bool> Confirm(string message);
    Task DownloadAs(MemoryStream stream, string defaultFileName, string mimeType);
}
