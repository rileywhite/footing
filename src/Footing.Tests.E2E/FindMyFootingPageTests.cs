using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

[Collection("Playwright")]
public class FindMyFootingPageTests
{
    private readonly PlaywrightFixture _fixture;
    public FindMyFootingPageTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    // One session per viewport, seeded through the context rather than by navigating to
    // "/" and writing localStorage first (W-01): StorageState puts the key in place before
    // the first navigation, so the app reads it on its initial render and the extra
    // round-trip to the landing page disappears.
    private async Task<PageSession> OpenToolPageAsync(Viewport viewport, bool withExistingData = false)
    {
        var session = await _fixture.NewSessionAsync(
            viewport,
            localStorageSeed: withExistingData ? ToolStorage.ReturningUser() : null);

        await session.Page.GotoAsync($"{_fixture.BaseUrl}{SitePage.Tool}");
        // WASM interactive component needs time to download and initialize
        await session.Page.WaitForSelectorAsync(
            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
        return session;
    }

    // D-02 on the tool page, at every viewport in the matrix.
    //
    // 320 and 375 were split out of here while F-12's overflow was reproduced and unrepaired:
    // the contract's overflow half did not hold there, so the gutter half was asserted by a
    // separate narrow-viewport test and the overflow half was quarantined in
    // NarrowViewportOverflowTests. W-06 repaired the overflow, so the whole contract holds at
    // all four widths again and the split is gone -- AssertLayoutContractAsync already covers
    // the gutter at every viewport, which is what the separate narrow test was for.
    [SkippableTheory]
    [MemberData(nameof(Viewports.All), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_LayoutContractHolds(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport);
        await SitePage.AssertLayoutContractAsync(session.Page, viewport, SitePage.Tool);
    }

    // W-05's ruling on both OQ-01 hypotheses, and the returning-user coverage W-04 could not
    // reach, live in NarrowViewportOverflowTests -- including the expanded returning-user state,
    // which is the one narrow case this class's first-time-user fixture does not reach.

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_LoadsWithTitle(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport);
        (await session.Page.Locator("h1").TextContentAsync()).Should().Contain("Manage My Money");
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_ShowsFiveMoneyFlowCards(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, withExistingData: true);
        (await session.Page.Locator("#moneyFlows > .card").CountAsync()).Should().BeGreaterThanOrEqualTo(5);
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_ShowsIncomeSection(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport);
        (await session.Page.Locator("#incomeHeading").CountAsync()).Should().Be(1);
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_ShowsNetTotal(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport);
        (await session.Page.Locator("#totalHeading").TextContentAsync()).Should().Contain("Net Total");
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_ShowsExportButton(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport);
        (await session.Page.Locator("input[value='Download Excel Spreadsheet']").CountAsync()).Should().Be(1);
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_CanExpandIncomeSection(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, withExistingData: true);
        await session.Page.Locator("#incomeHeading button").ClickAsync();
        await session.Page.WaitForSelectorAsync("#incomeDetails",
            new() { Timeout = 5000, State = WaitForSelectorState.Attached });
        (await session.Page.Locator("#incomeDetails button[type='submit']").CountAsync()).Should().Be(1);
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_CanAddIncomeItem(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, withExistingData: true);
        await session.Page.Locator("#incomeHeading button").ClickAsync();
        await session.Page.WaitForSelectorAsync("#incomeDetails",
            new() { Timeout = 5000, State = WaitForSelectorState.Attached });

        await session.Page.Locator("#incomeDetails input[placeholder='xxx.xx']").FillAsync("1000");
        await session.Page.Locator("#incomeDetails select").SelectOptionAsync("Weekly");
        await session.Page.Locator("#incomeDetails input[placeholder='Income Description']").FillAsync("Test Salary");
        await session.Page.Locator("#incomeDetails button[type='submit']").ClickAsync();

        await session.Page.WaitForSelectorAsync("#incomeDetails .ft-entry-chip");
        (await session.Page.Locator("#incomeDetails .ft-entry-list").TextContentAsync()).Should().Contain("Test Salary");
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_ShowsPrivacyNotice(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport);
        (await session.Page.Locator("em").First.TextContentAsync())
            .Should().Contain("Nothing you put in here will be sent to our servers");
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_HasClearLink(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport);
        (await session.Page.Locator("a:has-text('clear')").CountAsync()).Should().Be(1);
    }

    [SkippableTheory]
    [MemberData(nameof(Viewports.Full), MemberType = typeof(Viewports))]
    public async Task FindMyFooting_ShowsAllCategorySections(Viewport viewport)
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(viewport, withExistingData: true);
        foreach (var section in new[] { "income", "recurringBills", "householdBudgets", "personalBudgets", "eventBudgets" })
            (await session.Page.Locator($"#{section}Heading").CountAsync()).Should().Be(1, $"section {section} should exist");
    }
}
