using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

[Collection("Playwright")]
public class HomePageTests
{
    private readonly PlaywrightFixture _fixture;
    public HomePageTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    // D-01 single-viewport exemption: this asserts an HTTP status code. The status of
    // GET / does not vary with viewport size, so the matrix would buy three more browser
    // sessions and no additional coverage. Desktop is the arbitrary-but-named choice.
    [SkippableFact]
    public async Task HomePage_LoadsSuccessfully()
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Desktop);
        var response = await session.Page.GotoAsync(_fixture.BaseUrl);
        response!.Status.Should().Be(200);
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task HomePage_HasFindMyFootingLink(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(viewport);
        await session.Page.GotoAsync(_fixture.BaseUrl);
        var links = session.Page.Locator("a[href='find-my-footing/']");
        await links.First.WaitForAsync();
        (await links.CountAsync()).Should().BeGreaterThan(0);
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task HomePage_NavigatesToFindMyFooting(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(viewport);
        await session.Page.GotoAsync(_fixture.BaseUrl);
        await session.Page.Locator("a[href='find-my-footing/']").First.ClickAsync();
        await session.Page.WaitForURLAsync("**/find-my-footing/");
        session.Page.Url.Should().Contain("find-my-footing/");
    }

    // D-02, on the landing page, over all four viewports including the 320 floor.
    // SitePage.AssertLayoutContractAsync holds the (a)/(b)/(c) split and the rule about
    // when cap-and-centre applies; FindMyFootingPageTests runs the identical contract
    // against the tool page.
    [SkippableTheory]
    [MemberData(nameof(Viewports.All), MemberType = typeof(Viewports))]
    public async Task HomePage_LayoutContractHolds(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(viewport);
        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, SitePage.Landing);
        await SitePage.AssertLayoutContractAsync(session.Page, viewport, SitePage.Landing);
    }

    // D-01 single-viewport exemption: `display: none` is not viewport-dependent, and
    // #blazor-error-ui carries no responsive rules at all -- the same computed value would
    // be read at all four sizes.
    [SkippableFact]
    public async Task ErrorUi_IsSingleAndHidden()
    {
        // "/" is the static landing page now -- it has no Blazor app and no
        // #blazor-error-ui at all, so it's not part of this check anymore.
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Desktop);
        await session.Page.GotoAsync($"{_fixture.BaseUrl}{SitePage.Tool}");
        var errorUi = session.Page.Locator("#blazor-error-ui");
        (await errorUi.CountAsync()).Should().Be(1);
        (await errorUi.EvaluateAsync<string>("el => getComputedStyle(el).display"))
            .Should().Be("none");
    }

    // Runs at all four viewports including the 320 floor (BR-12), and -- per plan-review
    // R-04, deliberately wider than BR-12/AC-04 as written -- on *both* pages rather than
    // only the landing page. The page where a third-party request would plausibly arrive is
    // the WASM app: it is the one with a build pipeline, a package graph and _framework
    // fetches, and it was the page never checked.
    //
    // The landing page's "Buy Me A Coffee" anchor
    // (https://www.buymeacoffee.com/rileywhite, src/Footing.Site/index.html) is markup only:
    // an unclicked <a href> issues no request, so it does not register here and is not an
    // exemption this assertion has to carve out.
    [SkippableTheory]
    [MemberData(nameof(Viewports.AllByPage), MemberType = typeof(Viewports))]
    public async Task PageLoad_IssuesNoThirdPartyRequests(Viewport viewport, string page)
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(viewport);
        var requestUrls = new List<string>();
        session.Page.Request += (_, request) => requestUrls.Add(request.Url);

        await SitePage.GotoRenderedAsync(
            session.Page, _fixture.BaseUrl, page,
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var baseOrigin = new Uri(_fixture.BaseUrl).GetLeftPart(UriPartial.Authority);
        var thirdParty = requestUrls
            .Where(url => url.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            .Where(url => new Uri(url).GetLeftPart(UriPartial.Authority) != baseOrigin)
            .ToList();

        thirdParty.Should().BeEmpty(
            $"no third-party requests should be issued from {page} at {viewport}, but saw: {string.Join(", ", thirdParty)}");
    }

    // D-01 single-viewport exemption: font loading is viewport-invariant -- @font-face has
    // no width conditions here -- and the width probe below is the slowest assertion in the
    // suite, so running it four times would cost real gate time for no new information.
    [SkippableFact]
    public async Task PageLoad_RendersSelfHostedRubik()
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Desktop);
        var requestUrls = new List<string>();
        session.Page.Request += (_, request) => requestUrls.Add(request.Url);

        await session.Page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await session.Page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");

        var rubikRequests = requestUrls
            .Where(url => url.Contains("rubik", StringComparison.OrdinalIgnoreCase)
                && url.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
            .ToList();

        rubikRequests.Should().NotBeEmpty(
            "the self-hosted Rubik woff2 must actually be fetched -- a font that silently fails to "
            + "load issues no request at all, which passes PageLoad_IssuesNoThirdPartyRequests vacuously");

        // Not getComputedStyle: that returns the declared stack whether or not Rubik ever loaded.
        // Not document.fonts.check(): it reports true even for a face the browser skipped.
        var faceLoaded = await session.Page.EvaluateAsync<bool>(
            "() => Array.from(document.fonts).some(f => f.family.includes('Rubik') && f.status === 'loaded')");

        faceLoaded.Should().BeTrue("a Rubik @font-face must reach status 'loaded'");

        // An unsupported format() keyword makes the browser skip the src and fall back silently,
        // so compare against the same stack with Rubik removed.
        var widthDelta = await session.Page.EvaluateAsync<double>("""
            () => {
                const probe = (family) => {
                    const el = document.createElement('div');
                    el.style.cssText = 'position:absolute;left:-9999px;top:-9999px;'
                        + 'white-space:nowrap;font-size:100px;font-family:' + family;
                    el.textContent = 'Handgloves 123';
                    document.body.appendChild(el);
                    const width = el.getBoundingClientRect().width;
                    el.remove();
                    return width;
                };
                const fallback = "-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
                return Math.abs(probe("'Rubik', " + fallback) - probe(fallback));
            }
            """);

        widthDelta.Should().BeGreaterThan(0.5,
            "text must render in Rubik, not in the fallback stack");
    }
}
