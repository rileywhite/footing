using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

public class PlaywrightFixture : IAsyncLifetime
{
    private WebApplication? _app;

    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = null!;
    public bool ServerAvailable { get; private set; }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool PlaywrightRequired =>
        Environment.GetEnvironmentVariable("PLAYWRIGHT_REQUIRED") is { Length: > 0 } value
        && value != "0"
        && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        var port = FindFreePort();
        BaseUrl = $"http://localhost:{port}";

        var clientProjectPath = FindClientProject();
        if (!await PublishAsync(clientProjectPath))
        {
            if (PlaywrightRequired)
                throw new InvalidOperationException("PLAYWRIGHT_REQUIRED is set but publishing Footing.Client failed.");
            ServerAvailable = false;
            return;
        }

        var publishRoot = Path.Combine(
            Path.GetDirectoryName(clientProjectPath)!,
            "bin", "Release", "net10.0", "publish", "wwwroot");
        if (!Directory.Exists(publishRoot))
        {
            if (PlaywrightRequired)
                throw new InvalidOperationException($"PLAYWRIGHT_REQUIRED is set but the publish output was not found at '{publishRoot}'.");
            ServerAvailable = false;
            return;
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(BaseUrl);
        _app = builder.Build();

        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings[".wasm"] = "application/wasm";
        contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
        contentTypeProvider.Mappings[".blat"] = "application/octet-stream";
        contentTypeProvider.Mappings[".woff2"] = "font/woff2";

        var fileProvider = new PhysicalFileProvider(publishRoot);
        _app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ContentTypeProvider = contentTypeProvider,
        });
        _app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            FileProvider = fileProvider,
            ContentTypeProvider = contentTypeProvider,
        });

        try
        {
            await _app.StartAsync();
            ServerAvailable = true;
        }
        catch (Exception ex)
        {
            ServerAvailable = false;
            if (PlaywrightRequired)
                throw new InvalidOperationException("PLAYWRIGHT_REQUIRED is set but the test server failed to start.", ex);
            return;
        }

        try
        {
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        catch (Exception ex)
        {
            ServerAvailable = false;
            if (PlaywrightRequired)
                throw new InvalidOperationException("PLAYWRIGHT_REQUIRED is set but launching Chromium failed (is it installed?).", ex);
        }
    }

    private static async Task<bool> PublishAsync(string clientProjectPath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{clientProjectPath}\" -c Release",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return false;
        }

        await Task.WhenAll(stdoutTask, stderrTask);
        return process.ExitCode == 0;
    }

    private static string FindClientProject()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Footing.Client", "Footing.Client.csproj");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir, "src", "Footing.Client", "Footing.Client.csproj");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find Footing.Client.csproj");
    }

    public async Task DisposeAsync()
    {
        if (Browser != null) await Browser.DisposeAsync();
        Playwright?.Dispose();
        if (_app != null) await _app.DisposeAsync();
    }
}

[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture> { }
