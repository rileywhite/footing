using Deque.AxeCore.Playwright;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// W-12 settles OQ-04: which accessibility rule engine, obtained how.
///
/// BRANCH 1 OF THE ORDERED RULE WAS TAKEN. Deque.AxeCore.Playwright 4.13.0 resolves against
/// Microsoft.Playwright 1.58.0 on net10.0 with NO transitive downgrade -- verified with
/// `dotnet list package --include-transitive` and in project.assets.json, both of which still
/// show Microsoft.Playwright at 1.58.0. Branches 2 (vendoring a pinned axe.min.js) and 3
/// (hand-rolling the BR-21..BR-26 assertions) were therefore not needed and were not taken.
///
/// Why this satisfies the standing constraints, and how these tests prove it rather than
/// assert it:
///
///  * NOTHING SHIPS. The package is a test-project PackageReference. Footing.Client gains no
///    reference, script, style, font or asset; the published wwwroot is byte-identical.
///  * NOTHING IS FETCHED. axe-core is embedded in Deque.AxeCore.Commons.dll as a resource and
///    injected into the page at run time. There is no CDN in any branch, ever (TS-09, OOS-03,
///    BR-28, and CLAUDE.md's standing constraint). RunningAxe_IssuesNoThirdPartyRequests below
///    proves this from the browser's side rather than taking the package's word for it.
///
/// W-13 and W-14 build on this.
/// </summary>
[Collection("Playwright")]
public class AccessibilityEngineSmokeTests
{
    private readonly PlaywrightFixture _fixture;
    public AccessibilityEngineSmokeTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    [SkippableFact]
    public async Task AxeEngine_InjectsAndReturnsAResult()
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Desktop);
        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, SitePage.Landing);

        var result = await session.Page.RunAxe();

        result.Should().NotBeNull("the engine must return a result object");
        result.TestEngine.Name.Should().Be("axe-core");
        result.TestEngine.Version.Should().NotBeNullOrWhiteSpace();

        // The engine ran and classified something. Not asserting zero violations here --
        // that is W-13's and W-14's job, and this item is a spike about obtaining the engine.
        (result.Violations.Length + result.Passes.Length).Should().BeGreaterThan(
            0, "axe must actually have evaluated rules against the page");
    }

    // The constraint that matters most, proven from the browser rather than assumed from the
    // packaging: running the engine must not cause the page to reach off-origin. If a future
    // version of the package ever switched to fetching axe from a CDN, this fails.
    [SkippableFact]
    public async Task RunningAxe_IssuesNoThirdPartyRequests()
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Desktop);
        await SitePage.GotoRenderedAsync(
            session.Page, _fixture.BaseUrl, SitePage.Landing,
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Recording starts AFTER navigation, so only what the scan itself causes is counted.
        var requestUrls = new List<string>();
        session.Page.Request += (_, request) => requestUrls.Add(request.Url);

        await session.Page.RunAxe();

        var baseOrigin = new Uri(_fixture.BaseUrl).GetLeftPart(UriPartial.Authority);
        var thirdParty = requestUrls
            .Where(url => url.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            .Where(url => new Uri(url).GetLeftPart(UriPartial.Authority) != baseOrigin)
            .ToList();

        thirdParty.Should().BeEmpty(
            "the rule engine must be injected from the package, never fetched -- "
            + $"but saw: {string.Join(", ", thirdParty)}");
    }
}
