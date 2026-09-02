using FluentAssertions;
using Xunit;

namespace Footing.Tests.E2E;

[Collection("Playwright")]
public class StaticSiteCompositionTests
{
    private readonly PlaywrightFixture _fixture;
    public StaticSiteCompositionTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    // Guards the recursive composition of src/Footing.Site/ into the served output
    // (PlaywrightFixture.cs) against silently diverging from deploy-pages.yml's
    // `cp -r src/Footing.Site/. "$publish_dir/"` again: every file under
    // Footing.Site, at any depth, must be reachable over HTTP at its relative path.
    // .e2e-fixture/nested-probe.txt exists purely to give this a nested path to
    // check -- Footing.Site was flat until now, so a non-recursive copy would pass
    // undetected without it.
    [SkippableFact]
    public async Task AllStaticSiteFiles_AreServedAtTheirRelativePath()
    {
        SkipIfUnavailable();
        using var client = new HttpClient();

        var files = Directory.GetFiles(_fixture.SiteDirectory, "*", SearchOption.AllDirectories);
        files.Should().Contain(f => f.Contains(Path.Combine(".e2e-fixture", "nested-probe.txt")),
            "the nested probe fixture should exist under Footing.Site");

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(_fixture.SiteDirectory, file).Replace('\\', '/');
            var response = await client.GetAsync($"{_fixture.BaseUrl}/{relativePath}");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, $"'{relativePath}' should be served from the composed static site");
        }
    }
}
