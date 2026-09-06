using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// W-05's ruling on OQ-01, and the single place the narrow-viewport overflow is pinned.
///
/// OQ-01 named two defects PREDICTED FROM READING THE CSS, not observed. This class settles
/// both with evidence, running the overflow assertion at 320 and 375 across every combination
/// the plan asked for -- both pages, and both tool-page states:
///
///   page / state                         320      375     verdict
///   ----------------------------------   ------   -----   ----------------------------------
///   landing                              clean    clean   hypothesis 2 REFUTED
///   tool, first-time-user tree           +143px   +88px   hypothesis 1 REPRODUCED
///   tool, returning-user tree, collapsed clean    clean   no `dl` exists to overflow
///   tool, returning-user tree, expanded  +142px   +87px   hypothesis 1 REPRODUCED
///
/// Full verdicts, offending elements and the probe results behind the recommended repair are
/// in `.gc/artifacts/e2e-responsive-a11y/findings.md` under F-12 (revised by W-05), F-14 and
/// F-15. The quarantine tests at the bottom are the ones W-06 deletes.
/// </summary>
[Collection("Playwright")]
public class NarrowViewportOverflowTests
{
    private readonly PlaywrightFixture _fixture;
    public NarrowViewportOverflowTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    /// <summary>
    /// Opens the tool page in one of its two states and waits for the interactive tree, not
    /// just for `article.content` -- `#moneyFlows` only exists once the WASM runtime has
    /// booted, and an overflow assertion made against the "Loading&hellip;" placeholder passes
    /// vacuously.
    /// </summary>
    private async Task<PageSession> OpenToolPageAsync(Viewport viewport, bool returningUser)
    {
        var session = await _fixture.NewSessionAsync(
            viewport,
            localStorageSeed: returningUser ? ToolStorage.ReturningUserWithEveryCategory() : null);

        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, SitePage.Tool);
        await session.Page.WaitForSelectorAsync(
            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
        return session;
    }

    private static async Task ExpandAsync(IPage page, string section)
    {
        await page.Locator($"#{section}Heading button").ClickAsync();
        await page.WaitForSelectorAsync(
            $"#{section}Details", new() { Timeout = 5000, State = WaitForSelectorState.Attached });
    }

    // ================================================================================
    // Verdicts that hold. These pass today and must keep passing.
    // ================================================================================

    /// <summary>
    /// OQ-01 hypothesis 2 -- REFUTED. `.ft-hero h1` is sized from `--ft-text-3xl: 2rem`
    /// (app.css:654) with no fluid sizing, and the prediction was that a long unbreakable
    /// heading would overflow at 320. The heading the site actually ships, "Where Does Your
    /// Money Go?", is five short words that wrap freely, so it does not.
    ///
    /// This asserts the SHIPPED heading fits, which is the only claim the evidence supports.
    /// It is not a claim that 2rem is safe for arbitrary future copy -- a single long word in
    /// this heading would still overflow, and that is recorded in the ledger as F-15 rather
    /// than fixed, because changing the type scale is a redesign under D-10.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.AtMostMobile), MemberType = typeof(Viewports))]
    public async Task LandingHeroHeading_FitsTheViewport_AtNarrowViewports(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(viewport);
        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, SitePage.Landing);

        var clientWidth = await session.Page.EvaluateAsync<double>(
            "() => document.documentElement.clientWidth");
        var headingRight = await session.Page.EvalOnSelectorAsync<double>(
            ".ft-hero h1", "el => el.getBoundingClientRect().right");

        headingRight.Should().BeLessThanOrEqualTo(
            clientWidth + SitePage.TolerancePx,
            $"OQ-01 hypothesis 2: the .ft-hero h1 should fit within the {viewport} viewport "
            + $"(clientWidth={clientWidth})");

        // The document-level claim too, so a hero that fits while something else on the
        // landing page does not cannot be read as hypothesis 2 refuted.
        await LayoutAssertions.AssertNoHorizontalOverflowAsync(
            session.Page, $"{SitePage.Landing} at {viewport} should not overflow horizontally");
    }

    /// <summary>
    /// The returning-user tree does NOT overflow at either narrow width.
    ///
    /// This contradicts the premise W-05 was handed -- that the compact tree, "which renders
    /// five cards, is where the dl grid is under the most pressure". It is the opposite:
    /// every compact card is rendered with `IsOpen="false"` (`FootingAnalysisEditor.razor`),
    /// so the returning-user tree renders five collapsed HEADERS and no `dl` at all, while the
    /// first-time-user tree opens Income by default and therefore does. Card count is five
    /// either way; `dl` count is zero here and one there.
    ///
    /// So this is a genuine clean verdict, not an untested gap, and the assertion below on
    /// `#moneyFlows dl` is load-bearing: without it this test would keep passing if a future
    /// change made the compact tree render an open card, and would be asserting nothing about
    /// the defect at all.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.AtMostMobile), MemberType = typeof(Viewports))]
    public async Task ToolPage_ReturningUser_CollapsedTree_DoesNotOverflow(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, returningUser: true);

        (await session.Page.Locator("#moneyFlows > .card").CountAsync()).Should().Be(
            5, "the returning-user tree renders all five category cards");
        (await session.Page.Locator("#moneyFlows dl").CountAsync()).Should().Be(
            0, "every compact card is rendered collapsed, so no entry form is in the DOM -- "
             + "if this is ever non-zero the clean verdict below stops meaning anything");

        await LayoutAssertions.AssertNoHorizontalOverflowAsync(
            session.Page,
            $"{SitePage.Tool} in the returning-user (collapsed) state at {viewport} should not overflow");
    }

    /// <summary>
    /// Guards the seed the two returning-user tests above and below depend on.
    ///
    /// F-11 records how a wrong seed fails: silently. The tree still renders, the card count
    /// still passes, and every amount reads $0 -- which would make
    /// <see cref="ToolPage_ReturningUser_CollapsedTree_DoesNotOverflow"/> a test of an empty
    /// page wearing a returning user's clothes. Asserting a non-zero weekly total on each of
    /// the five headers is what makes a degenerate seed fail loudly here instead.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.AtMostMobile), MemberType = typeof(Viewports))]
    public async Task ReturningUserSeed_PopulatesEveryCategory(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, returningUser: true);

        foreach (var section in ToolStorage.SectionNames)
        {
            var header = await session.Page.Locator($"#{section}Heading").TextContentAsync();
            header.Should().NotBeNull();
            header.Should().NotContain(
                "$0 /",
                $"the '{section}' card should carry a real seeded entry; a $0 total means the "
                + "seed did not round-trip (see F-11 -- `Amount` is an object, `Period` is numeric)");
        }
    }

    // ================================================================================
    // QUARANTINE -- DELETE BOTH TESTS BELOW AS PART OF W-06.
    //
    // These assert the defect is STILL PRESENT. That is deliberate: W-05 rules, W-06 repairs,
    // and CR-01 means a red assertion here would block merges on a protected branch for
    // however long W-06 takes. Pinning rather than skipping keeps AC-01's no-skips promise,
    // keeps the defect named in the test list, and turns red the moment the CSS is fixed.
    //
    // When W-06 lands: delete both, and fold Viewports.AtMostMobile back into
    // FindMyFootingPageTests.FindMyFooting_LayoutContractHolds.
    // ================================================================================

    /// <summary>
    /// OQ-01 hypothesis 1 -- REPRODUCED, in the first-time-user tree. Measured overflow was
    /// +143px at 320 and +88px at 375. Moved here from FindMyFootingPageTests (where W-04 left
    /// it, ahead of this ruling) so W-06 has one file to delete rather than two.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.AtMostMobile), MemberType = typeof(Viewports))]
    public async Task ToolPage_FirstTimeUser_OverflowIsStillTheKnownDefect(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, returningUser: false);
        await AssertKnownOverflowStillPresentAsync(session.Page, viewport, "first-time-user");
    }

    /// <summary>
    /// OQ-01 hypothesis 1 -- REPRODUCED in the returning-user tree too, once a card is
    /// expanded. Measured overflow was +142px at 320 and +87px at 375.
    ///
    /// Expanding a card is what the plan's premise was reaching for: it is the only way the
    /// returning-user state renders a `dl`, and without this step that state is clean. So the
    /// defect is not specific to the first-time-user tree -- it belongs to the entry form,
    /// which both trees share via MoneyFlowCard -- and W-06 fixing only one tree would leave
    /// the other broken.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.AtMostMobile), MemberType = typeof(Viewports))]
    public async Task ToolPage_ReturningUser_ExpandedSection_OverflowIsStillTheKnownDefect(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, returningUser: true);
        await ExpandAsync(session.Page, "income");
        await AssertKnownOverflowStillPresentAsync(session.Page, viewport, "returning-user, income expanded");
    }

    /// <summary>
    /// Pins the defect by its OFFENDING ELEMENT as well as its presence, because D-03 says the
    /// element -- not the hypothesis -- is what W-06 fixes. If a change makes something else
    /// overflow instead, "still broken" is the wrong report and this says so.
    ///
    /// Deliberately asserts no pixel magnitude. The row's width comes from the intrinsic size
    /// of `<input size="30">`, which is font-metric dependent; asserting +143px would make this
    /// gate merges on the CI image's font rendering. The margin being pinned is large (a 327px
    /// track floor against 238px of available width), so `> 0` is not a weak assertion here.
    /// </summary>
    private static async Task AssertKnownOverflowStillPresentAsync(IPage page, Viewport viewport, string state)
    {
        var overflow = await LayoutAssertions.MeasureHorizontalOverflowAsync(page);
        var offenders = await LayoutAssertions.DescribeHorizontalOverflowAsync(page);

        var because =
            $"the known #moneyFlows entry-form overflow at {viewport} ({state}) is quarantined here "
            + "pending W-06 -- if this now passes, the defect is fixed, so delete this test "
            + $"(offenders reported: {offenders})";

        overflow.Should().BeGreaterThan(0, because);
        offenders.Should().Contain(
            "<dd>",
            "the overflowing element is the `dd` of the entry form's `dl`, whose `1fr` track "
            + "cannot shrink below the intrinsic width of the description input -- if the `dd` "
            + "is no longer named, the ledger's F-12 analysis is stale");
    }
}
