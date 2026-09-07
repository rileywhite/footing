using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// BR-09/BR-10: the landing page and the tool page must render the same chrome.
///
/// Since #64/#65 both pages share one stylesheet -- src/Footing.Site/index.html links
/// find-my-footing/app.css, the same file the Blazor app loads. #64 fixed #62 by promoting
/// MainLayout.razor.css's scoped rules (compiled to [b-xxxxx] selectors that a hand-written
/// static page can never match) into unscoped app.css. The tell for a recurrence is
/// *partial* styling -- some chrome elements keep their rules, others silently lose them --
/// which is why the comparison below is per element and per property rather than aggregate.
/// </summary>
[Collection("Playwright")]
public class SharedChromeTests
{
    private readonly PlaywrightFixture _fixture;
    public SharedChromeTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    /// <summary>
    /// The chrome both pages share. The footer is deliberately absent -- see finding F-01:
    /// the tool page has no footer element at all, and the landing page's
    /// &lt;footer class="ft-landing-footer"&gt; sits inside article.content inside &lt;main&gt;,
    /// so it is not a contentinfo landmark either. There is nothing on the tool page to
    /// compare it against. F-01 is reported here, not repaired -- giving the tool page a
    /// footer is a redesign (it changes what a sighted user sees), which under D-10 is
    /// Riley's call, not this suite's.
    /// </summary>
    private static readonly string[] ChromeSelectors =
    [
        "header.site-header",
        "nav.site-nav",
        ".site-wordmark",
        ".site-nav-link",
        ".theme-toggle",
    ];

    private static readonly string[] ComparedProperties =
    [
        "background-color",
        "color",
        "font-family",
        "font-size",
        "font-weight",
        "height",
        "padding-top",
        "padding-right",
        "padding-bottom",
        "padding-left",
        "border-bottom-width",
        "border-bottom-color",
        "position",
        "display",
        "align-items",
        "justify-content",
        "max-width",
        "margin-left",
        "margin-right",
    ];

    private async Task<PageSession> OpenAsync(Viewport viewport, string path)
    {
        var session = await _fixture.NewSessionAsync(viewport);
        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, path);

        // Freeze transitions before measuring anything. `.site-nav-link` declares
        // `transition: color 0.15s, background-color 0.15s` (app.css:328), and
        // getComputedStyle returns the INTERPOLATED value while a transition is running --
        // so removing .active below and reading `color` immediately yields a colour that is
        // neither the active nor the resting one (observed: rgb(90,122,106) settling to
        // rgb(110,125,114) ~150ms later). Sleeping past it would work and would be a flake
        // waiting to happen on a loaded CI runner (CR-01); killing the transition removes
        // the race instead. Applied identically to both pages, and `transition` is not one
        // of the compared properties, so this cannot mask a real difference.
        await session.Page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = "*, *::before, *::after { transition: none !important; animation: none !important; }",
        });

        // NavMenu renders the tool-page link as a <NavLink Match="NavLinkMatch.All" href="">,
        // so on /find-my-footing/ it carries .active and picks up the
        // `.site-nav-link:hover, .site-nav-link.active` rule (app.css:331) -- a different
        // color and background-color. That is a real, intended difference: the current page
        // is supposed to look current. Rather than dropping color and background-color from
        // the comparison for every element, which would blind it to exactly the #62 failure
        // mode, the link is compared in its non-active state on both sides by removing the
        // class in the DOM before measuring. This is a test-time mutation of the rendered
        // page only; nothing in the app changes.
        await session.Page.EvaluateAsync(
            "() => document.querySelectorAll('.site-nav-link.active').forEach(el => el.classList.remove('active'))");

        return session;
    }

    /// <summary>
    /// Compares each chrome element between the two pages, property by property, and returns
    /// one description per differing property. Returns a list rather than asserting so the
    /// mutation probe below can assert the opposite -- that a mismatch IS reported -- against
    /// the same code path the real comparison uses.
    /// </summary>
    private static async Task<IReadOnlyList<string>> CompareChromeAsync(IPage landing, IPage tool)
    {
        var mismatches = new List<string>();

        foreach (var selector in ChromeSelectors)
        {
            var landingStyles = await LayoutAssertions.ReadComputedStylesAsync(landing, selector, ComparedProperties);
            var toolStyles = await LayoutAssertions.ReadComputedStylesAsync(tool, selector, ComparedProperties);

            foreach (var property in ComparedProperties)
            {
                if (landingStyles[property] != toolStyles[property])
                {
                    mismatches.Add(
                        $"{selector} {{{property}}}: / = '{landingStyles[property]}', "
                        + $"/find-my-footing/ = '{toolStyles[property]}'");
                }
            }
        }

        return mismatches;
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task SharedChrome_IsIdenticalOnBothPages(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var landing = await OpenAsync(viewport, SitePage.Landing);
        await using var tool = await OpenAsync(viewport, SitePage.Tool);

        var mismatches = await CompareChromeAsync(landing.Page, tool.Page);

        mismatches.Should().BeEmpty(
            $"the two pages share one stylesheet and must render the same chrome at {viewport}; "
            + $"differences: {string.Join(" | ", mismatches)}");
    }

    // Mutation probe for AC-07/AC-08's "demonstrably fails", without a golden image: neutralise
    // the promoted chrome rules on the landing page only -- exactly the shape of the scoped-CSS
    // loss in #62/#64, where .site-nav kept its markup but lost its layout -- and assert the
    // comparison notices. Desktop because that is where the promoted rules do the most work:
    // max-width plus `margin: 0 auto` produce a 208px computed margin the neutralised page
    // cannot have.
    [SkippableFact]
    public async Task ChromeComparison_ReportsMismatch_WhenPromotedChromeRulesAreLost()
    {
        SkipIfUnavailable();
        await using var landing = await OpenAsync(Viewports.Desktop, SitePage.Landing);
        await using var tool = await OpenAsync(Viewports.Desktop, SitePage.Tool);

        await landing.Page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = ".site-nav { height: auto; padding: 0; max-width: none; margin: 0; }",
        });

        var mismatches = await CompareChromeAsync(landing.Page, tool.Page);

        mismatches.Should().NotBeEmpty(
            "a page that has lost the promoted .site-nav rules must not compare equal to one that "
            + "still has them -- if this passes, the comparison would have slept through #62");
        mismatches.Should().Contain(
            m => m.StartsWith("nav.site-nav", StringComparison.Ordinal),
            "the mismatch must name the element that actually changed");
    }

    // The second half of the same proof, for the gutter assertion rather than the chrome
    // comparison: an edge-to-edge #62-shaped page must make AssertContentGutterAsync throw.
    // Mobile because that is where the gutter assertion is the only thing standing between a
    // broken page and a green build -- the cap-and-centre assertion is vacuous below 864px.
    [SkippableFact]
    public async Task ContentGutterAssertion_Throws_AgainstAnEdgeToEdgePage()
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Mobile);
        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, SitePage.Landing);

        await session.Page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = "main { max-width: none; padding: 0; }",
        });

        var act = async () => await LayoutAssertions.AssertContentGutterAsync(
            session.Page, SitePage.ContentSelector, SitePage.MinGutterPx, SitePage.TolerancePx);

        var thrown = await act.Should().ThrowAsync<Exception>(
            "an edge-to-edge page must fail the gutter assertion -- that is the whole point of it");
        thrown.Which.Message.Should().Contain(SitePage.ContentSelector);
    }
}
