using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
    public string SiteDirectory { get; private set; } = null!;

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

        // Mirrors deploy-pages.yml's "Prepare GitHub Pages output" step: the Blazor
        // publish only produces app/ (StaticWebAssetBasePath), so the checked-in static
        // site shell has to be composed in on top before serving, or / and the 404 shim
        // don't exist in this harness at all. Semantics being mirrored, exactly:
        // `cp -r src/Footing.Site/. "$publish_dir/"` -- recursive, contents-of (not the
        // directory itself), overwrite-on-conflict. Divergence here is caught by
        // StaticSiteCompositionTests, which serves every file under Footing.Site and
        // fails if any of them isn't reachable through this composed output.
        var siteDir = FindSiteDirectory(clientProjectPath);
        SiteDirectory = siteDir;
        foreach (var file in Directory.GetFiles(siteDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(siteDir, file);
            var destination = Path.Combine(publishRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
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
        _app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = fileProvider,
        });
        _app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ContentTypeProvider = contentTypeProvider,
        });

        // GitHub Pages serves exactly one 404.html, at the site root, for ANY missing
        // path (with a real 404 status) -- ASP.NET's MapFallbackToFile is a SPA
        // fallback (200, always index.html) and does not model that. Reproducing the
        // real shape matters here because production has no other coverage of this
        // seam: a deep link under /app/ (e.g. /app/find-my-footing) must hit the same
        // 404.html, whose own script (src/Footing.Site/404.html) then redirects the
        // browser to /app/?/find-my-footing, which UseDefaultFiles above resolves to
        // app/index.html. The redirect script's own logic is intentionally not
        // re-verified here -- that's covered by the standalone Node script described in
        // docs/spike-static-landing-page.md -- this just exercises it as production
        // plumbing, the same way a real deep link does.
        // A plain terminal middleware, not MapFallback/an endpoint: registering any
        // routing endpoint implicitly moves endpoint selection to the front of the
        // pipeline, which makes the static-file middleware above skip real files too
        // (it defers to an already-selected endpoint) -- verified by hand against a
        // minimal repro, not assumed.
        _app.Run(async context =>
        {
            var notFoundFile = fileProvider.GetFileInfo("404.html");
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/html";
            await using var stream = notFoundFile.CreateReadStream();
            await stream.CopyToAsync(context.Response.Body);
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

    private static string FindSiteDirectory(string clientProjectPath)
    {
        // Footing.Site is a sibling of Footing.Client under src/.
        var srcDir = Path.GetDirectoryName(Path.GetDirectoryName(clientProjectPath))!;
        var candidate = Path.Combine(srcDir, "Footing.Site");
        if (Directory.Exists(candidate)) return candidate;
        throw new InvalidOperationException($"Could not find Footing.Site directory at '{candidate}'");
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
