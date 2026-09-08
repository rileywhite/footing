using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// F-01, landing half: `footer.ft-landing-footer` is a SIBLING of `main` so that it exposes
/// the `contentinfo` landmark, and the move is visually neutral.
///
/// The landmark itself is asserted in
/// <see cref="StructuralAccessibilityTests.Landmarks_ArePresentAndUnique"/>, where the rest of
/// the landmark set lives. This file is the other half of the bargain, and the reason the
/// repair was allowed at all: it was authorised as a landmark fix, NOT as a redesign, so the
/// acceptance bar was that nothing a sighted user sees moves by a pixel. That bar is a claim
/// with numbers behind it, and these are the numbers.
///
/// WHY THIS CAN MOVE AT ALL. `main` carries `max-width: 54rem; margin: 0 auto; padding: 0 1rem`
/// (app.css) and the footer used to inherit that box by sitting inside it, in `article.content`.
/// Outside `main` it has none of it, and `border-top` -- the only thing the footer paints -- is
/// drawn on the border box, so padding cannot buy the box back. `.ft-landing-footer` therefore
/// reproduces `main`'s CONTENT box directly (`width: calc(100% - 2rem); max-width: 52rem;
/// margin: ... auto`), and `.ft-landing-content` zeroes `.content`'s `padding-bottom: 4rem`,
/// which used to fall below the footer and would otherwise fall above it.
///
/// THE BASELINE, measured on `a6ee739` with the footer still inside `main`, by the F-01 section
/// of `tools/RenderSweep`:
///
///     viewport   |  x  | width | value-section -> footer | link -> doc end
///     320x568    |  16 |  288  |           32            |       96
///     375x667    |  16 |  343  |           32            |       96
///     768x1024   |  16 |  736  |           32            |       96
///     1280x800   | 224 |  832  |           32            |       96
///
/// (RenderSweep reports the tail as footer-border-box -> document end, which read 64 there and
/// reads 0 here; see <see cref="TailBelowLinkPx"/> for why this file measures it from the link
/// instead. The document height it also reports -- 1310/1255/1028/1028 -- is unchanged, which
/// is the claim both forms are really making.)
///
/// Those are the literals pinned below. They are a LEDGER, not a fresh reading of the current
/// build: adjusting one to match something the page started doing is the failure this file
/// exists to prevent. Per OOS-01 there is no golden image anywhere near this -- these are
/// computed layout values, which is the form Riley's ruling allows.
///
/// The literals are paired with an equality assertion against `article.content`'s own box,
/// which is the same invariant stated without magic numbers ("the footer keeps main's content
/// box"). The pair is deliberate: the equality survives a root-font-size or scrollbar change
/// that would legitimately move both boxes together, and the literals catch a change that
/// moves both boxes in step but away from where the page used to be.
/// </summary>
[Collection("Playwright")]
public class LandingFooterTests
{
    private readonly PlaywrightFixture _fixture;
    public LandingFooterTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    private const string Footer = "footer.ft-landing-footer";

    /// <summary>`.ft-landing-footer { margin-top: 2rem }`, the gap it had as a child of `.content`.</summary>
    private const double GapAbovePx = 32;

    /// <summary>
    /// How much page there is below the footer's link: `.ft-landing-footer`'s own
    /// `padding-bottom: 2rem` plus the `.content { padding-bottom: 4rem }` that used to sit
    /// under the whole footer. 96px, before and after.
    ///
    /// Measured to the LINK rather than to the footer's border box, because the two layouts
    /// spend those 96px differently and only the link is a thing anyone can see. Before, the
    /// footer's box ended 2rem under the link and `.content` supplied the last 4rem; now the
    /// footer's own `padding-bottom: 6rem` carries both, because a last child's bottom MARGIN
    /// does not extend the scrollable area and the document came back 64px short when it was
    /// written that way. A border-box-relative number would read 64 then and 0 now and would
    /// be describing that bookkeeping rather than the page.
    /// </summary>
    private const double TailBelowLinkPx = 96;

    /// <summary>
    /// The baseline table above, as theory data. Every viewport in the suite, because the cap
    /// binds at Desktop and the gutter binds everywhere else, and a repair that got only one
    /// of the two right would pass a Desktop-only or Mobile-only check.
    /// </summary>
    public static IEnumerable<object[]> BaselineGeometry =>
        new[]
        {
            new object[] { Viewports.MobileFloor, 16d, 288d },
            new object[] { Viewports.Mobile, 16d, 343d },
            new object[] { Viewports.Tablet, 16d, 736d },
            new object[] { Viewports.Desktop, 224d, 832d },
        };

    private async Task<PageSession> OpenLandingAsync(Viewport viewport)
    {
        var session = await _fixture.NewSessionAsync(viewport);
        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, SitePage.Landing);
        await session.Page.Locator(Footer).WaitForAsync();
        return session;
    }

    /// <summary>
    /// The structural fact the landmark rests on, asserted where it can say WHY it matters.
    ///
    /// `Landmarks_ArePresentAndUnique` counts a `contentinfo` and would go red if this
    /// regressed, but it would report "expected 1, found 0" about a footer element that is
    /// still right there in the markup, which is exactly the shape of confusion F-01 caused
    /// the first time. This names the cause instead. D-01 single-viewport exemption: nesting
    /// is a property of the DOM and does not vary with viewport width.
    /// </summary>
    [SkippableFact]
    public async Task Footer_IsOutsideMain_WhichIsWhatMakesItALandmark()
    {
        SkipIfUnavailable();
        await using var session = await OpenLandingAsync(Viewports.Desktop);

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
            + "inside main/article/section/aside/nav it is generic. If this reports an "
            + "ancestor, the landing footer has been moved back inside the content tree and "
            + "the landmark is gone even though the element is still in the markup");
    }

    /// <summary>
    /// The horizontal half: the footer's BORDER box -- the box its `border-top` rule is drawn
    /// on, and that rule is the only thing this footer paints -- lands where it did inside
    /// `main`.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(BaselineGeometry))]
    public async Task Footer_KeepsMainsContentBox(Viewport viewport, double expectedX, double expectedWidth)
    {
        SkipIfUnavailable();
        await using var session = await OpenLandingAsync(viewport);

        var footer = await BoxAsync(session.Page, Footer);
        var content = await BoxAsync(session.Page, SitePage.ContentSelector);

        var where = $"the landing footer at {viewport}";

        // R-03's 2px tolerance, for sub-pixel layout rounding only. The failure this is
        // guarding against is losing main's box entirely, which is worth 16px at the gutter
        // and 448px at the desktop cap -- nowhere near hideable inside 2px.
        footer.X.Should().BeApproximately(
            expectedX, SitePage.TolerancePx,
            $"{where} sat at x={expectedX} before it was promoted out of main (F-01 baseline)");
        footer.Width.Should().BeApproximately(
            expectedWidth, SitePage.TolerancePx,
            $"{where} was {expectedWidth}px wide before it was promoted out of main (F-01 "
            + "baseline). A full-viewport width here means .ft-landing-footer lost its "
            + "max-width/width and the border-top rule is spanning the page edge to edge");

        footer.X.Should().BeApproximately(
            content.X, SitePage.TolerancePx,
            $"{where} must line up with article.content, which is still inside main -- that "
            + "is the same claim as the literals above, stated without them");
        footer.Width.Should().BeApproximately(
            content.Width, SitePage.TolerancePx,
            $"{where} must be exactly as wide as article.content, which is still inside main");
    }

    /// <summary>
    /// The vertical half. Measured against the block above the footer and against the end of
    /// the document rather than against `main`, because `main` is what the footer moved out
    /// of: a main-relative number means something different before and after the change and
    /// so cannot pin neutrality across it.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.All), MemberType = typeof(Viewports))]
    public async Task Footer_KeepsItsVerticalPlacement(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenLandingAsync(viewport);

        var footer = await BoxAsync(session.Page, Footer);
        var above = await BoxAsync(session.Page, ".ft-value-section");
        var link = await BoxAsync(session.Page, $"{Footer} a");
        var documentHeight = await session.Page.EvaluateAsync<double>(
            "() => document.documentElement.scrollHeight");

        var where = $"the landing footer at {viewport}";

        (footer.Top - above.Bottom).Should().BeApproximately(
            GapAbovePx, SitePage.TolerancePx,
            $"{where} sat {GapAbovePx}px below .ft-value-section before it was promoted out of "
            + "main. 64px more than that means .content's padding-bottom is now falling ABOVE "
            + "the footer -- .ft-landing-content is missing or was overridden");
        (documentHeight - link.Bottom).Should().BeApproximately(
            TailBelowLinkPx, SitePage.TolerancePx,
            $"{where} had {TailBelowLinkPx}px of document below its link before it was promoted "
            + "out of main. 64px short means the tail was written as the footer's margin-bottom "
            + "rather than its padding-bottom: a last child's bottom margin does not extend the "
            + "scrollable area, so the page ends flush against the footer's border box and the "
            + "document is shorter than it was");
    }

    private sealed record Box(double X, double Width, double Top, double Bottom);

    /// <summary>
    /// The element's border box in DOCUMENT coordinates, so the vertical numbers can be
    /// compared against `scrollHeight` without depending on the page not having scrolled.
    /// `ILocator.BoundingBoxAsync` is viewport-relative and would need that assumption.
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

        box.Should().NotBeNull($"{selector} should be present and rendered on the landing page");
        box!.Should().HaveCount(4);
        return new Box(box[0], box[1], box[2], box[3]);
    }
}
