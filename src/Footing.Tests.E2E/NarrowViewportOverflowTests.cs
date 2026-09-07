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
/// Full verdicts, offending elements and the probe results behind the repair are in
/// `.gc/artifacts/e2e-responsive-a11y/findings.md` under F-12 (revised by W-05), F-14 and F-15.
///
/// **W-06 has since repaired the reproduced defect** (`grid-template-columns: auto minmax(0,
/// 1fr)` on `#moneyFlows dl`, plus `min-width: 0; max-width: 100%` on the `dd`'s controls), so
/// the two quarantine tests that asserted it was STILL PRESENT are gone and the two states they
/// covered are asserted clean instead. The table above is left as written: it is the record of
/// what was measured before the repair, and the numbers in it are what the fix had to close.
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
    // The state the repair had to reach, now asserted positively.
    //
    // Two quarantine tests lived here until W-06: they asserted the overflow was STILL PRESENT,
    // so a reproduced defect could sit on a protected branch without skipping anything (CR-01,
    // AC-01) and would turn red the moment the CSS was fixed. It did -- all four cases failed
    // with "found 0" -- and they are deleted rather than inverted.
    //
    // The first-time-user tree's clean state is asserted by
    // FindMyFootingPageTests.FindMyFooting_LayoutContractHolds, which W-06 folded back to the
    // full viewport set. The returning-user EXPANDED state has no other home, so it is below.
    // ================================================================================

    /// <summary>
    /// The returning-user tree, with a card expanded, at the two narrow viewports.
    ///
    /// This is the state that made the defect's real shape visible: the overflow does not
    /// belong to the first-time-user tree, it belongs to the entry form both trees share via
    /// MoneyFlowCard, and it is only reachable here by expanding a card -- collapsed, this tree
    /// renders no `dl` at all (see the sibling test above). A repair validated against the
    /// first-time-user tree alone would have been half a repair, and this is what says so.
    ///
    /// The `dl` count assertion is load-bearing for the same reason it is in the collapsed
    /// test, inverted: if expanding ever stopped rendering the entry form, the overflow
    /// assertion below would pass while checking nothing.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Viewports.AtMostMobile), MemberType = typeof(Viewports))]
    public async Task ToolPage_ReturningUser_ExpandedSection_DoesNotOverflow(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, returningUser: true);
        await ExpandAsync(session.Page, "income");

        (await session.Page.Locator("#moneyFlows dl").CountAsync()).Should().Be(
            1, "expanding a card renders its entry form -- the thing that used to overflow; if "
             + "this is ever zero the assertion below is checking an empty page");

        await LayoutAssertions.AssertNoHorizontalOverflowAsync(
            session.Page,
            $"{SitePage.Tool} in the returning-user state at {viewport} with the income card "
            + "expanded should not overflow (F-12, repaired by W-06)");
    }
}
