using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// BR-17/BR-18: entered money flows survive a reload, and "Clear my data" actually clears
/// them -- including the dismiss path, where it must not.
/// </summary>
[Collection("Playwright")]
public class MoneyFlowPersistenceTests
{
    private const string StorageKey = ToolStorage.AnalysisKey;
    private const string ThemeKey = ToolStorage.ThemeKey;
    private const string SeededEntryName = ToolStorage.SeededEntryName;

    private readonly PlaywrightFixture _fixture;
    public MoneyFlowPersistenceTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    private async Task<PageSession> OpenToolPageAsync(IDictionary<string, string>? seed = null)
    {
        // Desktop throughout: none of this is viewport-dependent -- it is storage and dialog
        // behaviour -- and W-04 already runs the tool page across the matrix.
        var session = await _fixture.NewSessionAsync(Viewports.Desktop, localStorageSeed: seed);
        await session.Page.GotoAsync($"{_fixture.BaseUrl}{SitePage.Tool}");
        await session.Page.WaitForSelectorAsync(
            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
        return session;
    }

    private static Dictionary<string, string> SeededStorage() =>
        ToolStorage.ReturningUserWithTheme("dark");

    /// <summary>
    /// D-07: the save runs in OnAfterRenderAsync(firstRender: false), one render AFTER the
    /// mutation, so reloading straight after the click races the write. This is a bounded
    /// browser-side poll (Playwright re-evaluates until the predicate holds or the timeout
    /// expires), NOT a fixed sleep -- a sleep would be both slower and flakier, and this suite
    /// runs on the protected gate (CR-01).
    /// </summary>
    private static Task WaitForStoredAnalysisToContainAsync(IPage page, string fragment) =>
        page.WaitForFunctionAsync(
            """
            ([key, fragment]) => {
                const raw = localStorage.getItem(key);
                return raw !== null && raw.includes(fragment);
            }
            """,
            new[] { StorageKey, fragment },
            new PageWaitForFunctionOptions { Timeout = 15000 });

    /// <summary>
    /// True when the stored analysis holds no entries in any of the five categories.
    ///
    /// Deliberately NOT "the storage key is absent". ClearLocalStorage calls
    /// LocalStore.ClearAsync(), which does empty the origin -- but the re-render it triggers
    /// runs OnAfterRenderAsync(firstRender: false), which writes an EMPTY FootingAnalysis
    /// straight back under the same key. Observed immediately after an accepted clear:
    ///   {"Inflows":[],"RecurringBills":[],"HouseholdBudgets":[],"PersonalBudgets":[],
    ///    "EventBudgets":[],"WeeklyTotalMoneyFlow":0,"HasAnyEntries":false}
    /// and the same again after a reload. So a key-absence assertion would fail against
    /// correct user-visible behaviour. Emptiness is the invariant that actually matters, and
    /// it stays true either way -- including if the residual write is ever removed, since a
    /// missing key counts as cleared here.
    /// </summary>
    private static Task<bool> StoredAnalysisIsEmptyAsync(IPage page) =>
        page.EvaluateAsync<bool>(
            """
            key => {
                const raw = localStorage.getItem(key);
                if (raw === null) return true;
                const a = JSON.parse(raw);
                return [a.Inflows, a.RecurringBills, a.HouseholdBudgets, a.PersonalBudgets, a.EventBudgets]
                    .every(list => !list || list.length === 0);
            }
            """,
            StorageKey);

    [SkippableFact]
    public async Task EnteredIncome_SurvivesAReload()
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync();
        var page = session.Page;

        await page.Locator("#incomeDetails input[placeholder='xxx.xx']").FillAsync("1000");
        await page.Locator("#incomeDetails select").SelectOptionAsync("Weekly");
        await page.Locator("#incomeDetails input[placeholder='Income Description']").FillAsync("Reload Survivor");
        await page.Locator("#incomeDetails button[type='submit']").ClickAsync();

        await page.WaitForSelectorAsync("#incomeDetails .ft-entry-chip");
        await WaitForStoredAnalysisToContainAsync(page, "Reload Survivor");

        await page.ReloadAsync();
        await page.WaitForSelectorAsync(
            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });

        // The reload comes back as a returning user, and that tree renders every MoneyFlowCard
        // with IsOpen="false" -- #incomeDetails and its chips are not in the DOM at all until
        // the section is expanded. Waiting for .ft-entry-chip without expanding first just
        // times out; it is not evidence that the entry was lost.
        (await page.Locator("#moneyFlows.ft-compact-summary").CountAsync())
            .Should().Be(1, "an analysis with entries should reload as a returning user");
        await page.Locator("#incomeHeading button").ClickAsync();
        await page.WaitForSelectorAsync("#incomeDetails .ft-entry-chip", new() { Timeout = 15000 });

        (await page.Locator("#incomeDetails .ft-entry-list").TextContentAsync())
            .Should().Contain("Reload Survivor", "an entered money flow must survive a reload");
    }

    // D-06: Playwright AUTO-DISMISSES dialogs when nothing is listening, so a test that just
    // clicks "Clear my data" gets false back from JSInterop.Confirm, nothing is cleared, and
    // the test fails confusingly -- or worse, passes an assertion written against the wrong
    // expectation. The handler is registered BEFORE the click, and accepts explicitly.
    [SkippableFact]
    public async Task ClearMyData_WhenConfirmed_ClearsEnteredDataAndSurvivesReload()
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(SeededStorage());
        var page = session.Page;

        (await page.Locator("#moneyFlows.ft-compact-summary").CountAsync())
            .Should().Be(1, "the seeded analysis should open in the returning-user tree");

        page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await page.Locator("button.ft-clear-btn").First.ClickAsync();

        await page.WaitForSelectorAsync("#moneyFlows.ft-conversational", new() { Timeout = 15000 });
        (await StoredAnalysisIsEmptyAsync(page))
            .Should().BeTrue("accepting the confirm dialog must clear the entered money flows");

        await page.ReloadAsync();
        await page.WaitForSelectorAsync(
            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });

        (await StoredAnalysisIsEmptyAsync(page))
            .Should().BeTrue("the clear must survive a reload, not just repaint the page");
        (await page.Locator("#moneyFlows.ft-conversational").CountAsync())
            .Should().Be(1, "a cleared analysis should come back as a first-time user");
    }

    [SkippableFact]
    public async Task ClearMyData_WhenDismissed_LeavesDataIntact()
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(SeededStorage());
        var page = session.Page;

        // Dismissed explicitly rather than leaning on Playwright's auto-dismiss: the default
        // is what D-06 warns about, and a test whose outcome depends on an unstated default
        // stops meaning anything the day the default changes.
        page.Dialog += (_, dialog) => dialog.DismissAsync();
        await page.Locator("button.ft-clear-btn").First.ClickAsync();

        // The dismiss path changes nothing, so there is no state transition to wait for.
        // Wait for the storage key to still hold the seeded entry rather than sampling
        // immediately, so a hypothetical delayed clear would be caught rather than missed.
        await WaitForStoredAnalysisToContainAsync(page, SeededEntryName);

        (await StoredAnalysisIsEmptyAsync(page))
            .Should().BeFalse("dismissing the confirm dialog must leave the entered data alone");
        (await page.Locator("#moneyFlows.ft-compact-summary").CountAsync())
            .Should().Be(1, "a dismissed clear should leave the returning-user tree in place");
    }

    // Finding F-02, asserted rather than only written down, so it cannot quietly change
    // without someone noticing. LocalStore.ClearAsync() empties the WHOLE origin, so the
    // ft-theme preference dies with the financial data: clearing your finances silently
    // resets your theme. Confirmed here -- ft-theme is "dark" before the click and null
    // after, and stays null across a reload.
    //
    // This is reported, not repaired (D-10): scoping the clear to the analysis key alone
    // would change what a user experiences, which is Riley's call. If that call is ever
    // made, this test is the one to update -- it pins today's behaviour, not the desired one.
    [SkippableFact]
    public async Task ClearMyData_AlsoDiscardsTheThemePreference_F02()
    {
        SkipIfUnavailable();
        await using var session = await OpenToolPageAsync(SeededStorage());
        var page = session.Page;

        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", ThemeKey))
            .Should().Be("dark", "the seeded theme preference should be present before clearing");

        page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await page.Locator("button.ft-clear-btn").First.ClickAsync();
        await page.WaitForSelectorAsync("#moneyFlows.ft-conversational", new() { Timeout = 15000 });

        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", ThemeKey))
            .Should().BeNull(
                "F-02: ClearAsync() clears the whole origin, so the theme preference goes with "
                + "the financial data -- if this now fails, the clear was scoped and F-02 is fixed");
    }
}
