using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// F-01, tool half: the tool page carries the privacy line, in a `footer` that is a SIBLING of
/// `main` so that it also exposes the `contentinfo` landmark.
///
/// WHAT THIS IS ACTUALLY FOR. Read the landmark as the side effect it is. The tool page was
/// already fully wrapped in `header` + `main`, axe's `region` rule already passed, and no WCAG
/// success criterion requires a `contentinfo` at all -- there was no conformance defect here.
/// What there was: the privacy promise shipped only on the landing page, in
/// `.ft-privacy-note`, so the one page where somebody actually types their finances was the
/// one page that never made it, and anybody arriving at /find-my-footing/ from a bookmark or a
/// deep link never saw it. Riley authorised this footer on 2026-09-07 to close that gap. The
/// landmark came free with building it correctly. So the first thing asserted below is the
/// SENTENCE, and it is asserted verbatim: the wording was chosen deliberately over five
/// alternatives and "improving" it in passing is the regression this catches.
///
/// The landmark count itself lives with the rest of the landmark set, in
/// <see cref="StructuralAccessibilityTests.Landmarks_ArePresentAndUnique"/>, which no longer
/// branches per page now that both pages have one.
///
/// THE GEOMETRY, and why it needs pinning at all. `main` carries
/// `max-width: 54rem; margin: 0 auto; padding: 0 1rem`, and a footer inside it would inherit
/// that box -- but a `footer` element is `contentinfo` only at the top level of the document,
/// so it cannot be inside it. Outside, it has none of that box, and `border-top` -- the only
/// thing this footer paints -- is drawn on the BORDER box, which padding sits inside of, so
/// padding cannot buy the box back: the rule would run edge to edge, 832px -> 1280px at
/// desktop. `.ft-tool-footer` therefore reproduces `main`'s CONTENT box directly
/// (`width: calc(100% - 2rem); max-width: 52rem; margin: ... auto`), exactly as
/// `.ft-landing-footer` does, and `.ft-tool-content` zeroes `.content`'s `padding-bottom: 4rem`
/// so it falls below the footer rather than above it.
///
/// THE ONE THING THE LANDING FOOTER NEVER HAS TO SURVIVE. `.ft-sticky-total` is
/// `position: fixed; bottom: 0` on the tool page. The privacy line is now the bottom-most
/// thing in the document, so at the end of the scroll it is exactly where that bar sits, and
/// every geometry number above can read correctly while the sentence itself is hidden behind
/// it. That is what the 4rem carried down from `.content` is really for here, and
/// <see cref="PrivacyLine_IsNotCoveredByTheStickyTotalBar"/> is the assertion that says so.
/// It is also why the tail is `padding-bottom` and not `margin-bottom`: a last child's bottom
/// margin does not extend the scrollable area, so the clearance would not exist at the only
/// place it is needed.
///
/// Per OOS-01 there is no golden image near any of this -- computed layout values only, which
/// is the form Riley's ruling allows. The literals are paired with an equality assertion
/// against `article.content`'s own box, which is the same invariant without magic numbers:
/// the equality survives a root-font-size or scrollbar change that legitimately moves both
/// boxes together, and the literals catch a change that moves both in step but away from the
/// content column.
/// </summary>
[Collection("Playwright")]
public class ToolFooterTests
{
    private readonly PlaywrightFixture _fixture;
    public ToolFooterTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    private const string Footer = "footer.ft-tool-footer";
    private const string Line = $"{Footer} p";

    /// <summary>
    /// The wording, verbatim. Chosen over five alternatives and delegated to nobody
    /// downstream: present tense and "here" make it a fact about this session rather than a
    /// general product claim (the landing page's "never leaves your browser" is doing a
    /// different, persuasive job); "stays" is a presence rather than an absence, which suits a
    /// product whose landing page says "You can do this"; and "everything" covers the saved
    /// analysis and the Excel export, not just what is being typed. If this assertion fails
    /// because the sentence was reworded, the reword is the thing to revert.
    /// </summary>
    private const string PrivacyLine = "Everything here stays in your browser.";

    /// <summary>`.ft-tool-footer { margin-top: 2rem }`, measured from the end of `.content`.</summary>
    private const double GapAbovePx = 32;

    /// <summary>
    /// `main`'s content box at each viewport: `calc(100% - 2rem)` below the cap, and the 54rem
    /// cap less its two 1rem gutters above it. Every viewport in the suite, because the cap
    /// binds at Desktop and the gutter binds everywhere else, so a footer that got only one of
    /// the two right would pass a Desktop-only or a Mobile-only check.
    /// </summary>
    public static IEnumerable<object[]> ContentBoxGeometry =>
        new[]
        {
            new object[] { Viewports.MobileFloor, 16d, 288d },
            new object[] { Viewports.Mobile, 16d, 343d },
            new object[] { Viewports.Tablet, 16d, 736d },
            new object[] { Viewports.Desktop, 224d, 832d },
        };

    /// <summary>
    /// Opens the tool page in the returning-user state. That state is the one that matters
    /// here: it fills the page with cards, so the footer is reached by scrolling rather than
    /// sitting in view, which is the only condition under which the fixed net-total bar can
    /// cover anything.
    /// </summary>
    private async Task<PageSession> OpenToolAsync(Viewport viewport, string? theme = null)
    {
        var seed = ToolStorage.ReturningUserWithEveryCategory();
        if (theme is not null)
        {
            seed[ToolStorage.ThemeKey] = theme;
        }

        // D-04: the OS preference stays Light in both runs and dark is reached only through
        // the seeded `ft-theme` key, so a dark run cannot be quietly served by app.css's
        // `prefers-color-scheme` fallback.
        var session = await _fixture.NewSessionAsync(
            viewport, ColorScheme.Light, localStorageSeed: seed);

        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, SitePage.Tool);
        await session.Page.WaitForSelectorAsync(
            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
        await session.Page.Locator(Footer).WaitForAsync();
        return session;
    }

    /// <summary>
    /// The point of the change, asserted first because it is the point of the change.
    ///
    /// Every viewport: this is a one-line block inside a fixed-width column and there is no
    /// width at which it is allowed to be clipped away, collapsed to zero height, or hidden
    /// behind a narrow-viewport rule. `IsVisibleAsync` covers display/visibility; the height
    /// check covers the case where it is technically visible and has no box.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.All), MemberType = typeof(Viewports))]
    public async Task Footer_CarriesThePrivacyLine(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolAsync(viewport);

        var line = session.Page.Locator(Line);

        (await line.IsVisibleAsync()).Should().BeTrue(
            $"the privacy line must be visible on the tool page at {viewport} -- it is the "
            + "reason this footer exists, and a footer that renders without it is the defect "
            + "F-01's tool half was opened for");
        (await line.InnerTextAsync()).Trim().Should().Be(
            PrivacyLine,
            $"the wording at {viewport} was chosen deliberately (see PrivacyLine above) and is "
            + "not for downstream editing -- if this changed on purpose, change it here too "
            + "and say why");

        var height = await line.EvaluateAsync<double>("el => el.getBoundingClientRect().height");
        height.Should().BeGreaterThan(0, $"the privacy line must have a rendered box at {viewport}");
    }

    /// <summary>
    /// The structural fact the landmark rests on, asserted where it can say WHY it matters.
    ///
    /// `Landmarks_ArePresentAndUnique` counts a `contentinfo` and would go red if this
    /// regressed, but it would report "expected 1, found 0" about a footer element that is
    /// still right there in MainLayout.razor -- which is the exact shape of confusion F-01
    /// caused on the landing page the first time. This names the cause instead. D-01
    /// single-viewport exemption: nesting is a property of the DOM and does not vary with
    /// viewport width.
    /// </summary>
    [SkippableFact]
    public async Task Footer_IsOutsideMain_WhichIsWhatMakesItALandmark()
    {
        SkipIfUnavailable();
        await using var session = await OpenToolAsync(Viewports.Desktop);

        var nestedIn = await session.Page.EvaluateAsync<string?>(
            $$"""
            () => {
              const ancestor = document.querySelector('{{Footer}}')
                .closest('main, article, section, aside, nav');
              return ancestor ? ancestor.tagName.toLowerCase() : null;
            }
            """);

        nestedIn.Should().BeNull(
            "a footer element exposes contentinfo only at the top level of the document -- "
            + "inside main/article/section/aside/nav it is generic. A plain <div> ancestor "
            + "(index.html's #app) does not count and is fine. If this reports main or "
            + "article, the footer was moved inside MainLayout's <main> and the landmark is "
            + "gone even though the element is still in the markup");
    }

    /// <summary>
    /// The horizontal half: the footer's BORDER box -- the box its `border-top` is drawn on,
    /// and that rule is the only thing this footer paints -- lands on the content column
    /// rather than on the viewport edges.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(ContentBoxGeometry))]
    public async Task Footer_TakesMainsContentBox(Viewport viewport, double expectedX, double expectedWidth)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolAsync(viewport);

        var footer = await BoxAsync(session.Page, Footer);
        var content = await BoxAsync(session.Page, SitePage.ContentSelector);

        var where = $"the tool footer at {viewport}";

        // R-03's 2px tolerance, for sub-pixel layout rounding only. The failure being guarded
        // against is losing main's box entirely, which is worth 16px at the gutter and 448px
        // at the desktop cap -- nowhere near hideable inside 2px.
        footer.X.Should().BeApproximately(
            expectedX, SitePage.TolerancePx,
            $"{where} must start at x={expectedX}, where main's content box starts");
        footer.Width.Should().BeApproximately(
            expectedWidth, SitePage.TolerancePx,
            $"{where} must be {expectedWidth}px wide, which is main's content box. A "
            + "full-viewport width here means .ft-tool-footer lost its max-width/width and "
            + "the border-top rule is spanning the page edge to edge");

        footer.X.Should().BeApproximately(
            content.X, SitePage.TolerancePx,
            $"{where} must line up with article.content, which is still inside main -- that is "
            + "the same claim as the literals above, stated without them");
        footer.Width.Should().BeApproximately(
            content.Width, SitePage.TolerancePx,
            $"{where} must be exactly as wide as article.content, which is still inside main");
    }

    /// <summary>
    /// The vertical half. `.content`'s `padding-bottom: 4rem` is zeroed by `.ft-tool-content`
    /// and re-spent inside the footer, so the gap between the two is the footer's own 2rem
    /// margin and nothing else. 64px more than that means `.ft-tool-content` is missing or was
    /// overridden and the old bottom padding is falling ABOVE the footer.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.All), MemberType = typeof(Viewports))]
    public async Task Footer_SitsDirectlyBelowTheContentColumn(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolAsync(viewport);

        var footer = await BoxAsync(session.Page, Footer);
        var content = await BoxAsync(session.Page, SitePage.ContentSelector);

        (footer.Top - content.Bottom).Should().BeApproximately(
            GapAbovePx, SitePage.TolerancePx,
            $"the tool footer at {viewport} sits {GapAbovePx}px below article.content -- its "
            + "own margin-top. 64px more means .content's padding-bottom is still there and is "
            + "now falling above the footer instead of below it");
    }

    /// <summary>
    /// The assertion that is specific to THIS page. `.ft-sticky-total` is fixed to the bottom
    /// of the viewport, and the privacy line is the last thing in the document, so at the end
    /// of the scroll the two are competing for the same band of pixels. Nothing else in the
    /// suite asks this: RenderSweep's occlusion check considers only CONTROLS, and the line is
    /// a `p`.
    ///
    /// Both colour schemes, because "the user can read it" is the requirement and a scheme
    /// swap is a plausible way to lose it -- though the palette half of that is axe's job, in
    /// <see cref="ContrastTests"/>, which scans this text along with everything else on the
    /// page in both schemes.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(ViewportsAndSchemes))]
    public async Task PrivacyLine_IsNotCoveredByTheStickyTotalBar(Viewport viewport, string? theme)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolAsync(viewport, theme);

        await session.Page.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)");
        await session.Page.WaitForFunctionAsync(
            "() => Math.abs(window.scrollY + window.innerHeight - document.documentElement.scrollHeight) < 2 "
          + "|| document.documentElement.scrollHeight <= window.innerHeight");

        // Viewport coordinates on purpose, unlike BoxAsync: the bar is `position: fixed`, so
        // it has no meaningful document position and only the viewport frame can compare them.
        var measured = await session.Page.EvaluateAsync<double[]?>(
            $$"""
            () => {
              const line = document.querySelector('{{Line}}');
              const bar = document.querySelector('.ft-sticky-total');
              if (!line || !bar) return null;
              return [line.getBoundingClientRect().bottom, bar.getBoundingClientRect().top];
            }
            """);

        measured.Should().NotBeNull(
            "both the privacy line and the net-total bar should be present on the tool page in "
            + "the returning-user state");
        measured!.Should().HaveCount(2);

        var where = $"the privacy line at {viewport} in the {theme ?? "light"} scheme";
        measured[0].Should().BeLessThanOrEqualTo(
            measured[1],
            $"{where} must clear the fixed net-total bar when the page is scrolled to the very "
            + "bottom -- it ends at y={0} and the bar starts at y={1}. If this fails, the "
            + "footer's padding-bottom no longer covers the bar's height: that tail is 6rem, "
            + "2rem of its own plus the 4rem of clearance .content used to provide, and "
            + "shrinking it puts the one sentence this footer exists for underneath the bar");
    }

    /// <summary>Every viewport x both colour schemes, with dark seeded rather than emulated (D-04).</summary>
    public static IEnumerable<object[]> ViewportsAndSchemes =>
        from viewport in new[] { Viewports.MobileFloor, Viewports.Mobile, Viewports.Tablet, Viewports.Desktop }
        from theme in new string?[] { null, "dark" }
        select new object[] { viewport, theme! };

    private sealed record Box(double X, double Width, double Top, double Bottom);

    /// <summary>
    /// The element's border box in DOCUMENT coordinates, so vertical numbers do not depend on
    /// the page not having scrolled. `ILocator.BoundingBoxAsync` is viewport-relative and
    /// would need that assumption.
    ///
    /// Returned as a flat `double[]` rather than as an object: Playwright .NET cannot
    /// deserialize a JS object into a record or a dictionary, and it does not throw -- it
    /// hands back a default (F-13), which turns a measurement bug into an assertion that
    /// passes against zeroes. Arrays of primitives it does handle.
    /// </summary>
    private static async Task<Box> BoxAsync(IPage page, string selector)
    {
        var box = await page.EvaluateAsync<double[]?>(
            """
            selector => {
              const el = document.querySelector(selector);
              if (!el) return null;
              const r = el.getBoundingClientRect();
              return [r.x + window.scrollX, r.width, r.y + window.scrollY, r.bottom + window.scrollY];
            }
            """,
            selector);

        box.Should().NotBeNull($"{selector} should be present and rendered on the tool page");
        box!.Should().HaveCount(4);
        return new Box(box[0], box[1], box[2], box[3]);
    }
}
