using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

[Collection("Playwright")]
public class FootingMePageTests
{
    private readonly PlaywrightFixture _fixture;
    public FootingMePageTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    private async Task<IPage> NavigateToFootingMe()
    {
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/footing-me");
        // WASM interactive component needs time to download and initialize
        await page.WaitForSelectorAsync("#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
        return page;
    }

    [SkippableFact]
    public async Task FootingMe_LoadsWithTitle()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        (await page.Locator("h1").TextContentAsync()).Should().Contain("Manage My Money");
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_ShowsFiveMoneyFlowCards()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        (await page.Locator("#moneyFlows > .card").CountAsync()).Should().BeGreaterThanOrEqualTo(5);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_ShowsIncomeSection()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        (await page.Locator("#incomeHeading").CountAsync()).Should().Be(1);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_ShowsNetTotal()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        (await page.Locator("#totalHeading").TextContentAsync()).Should().Contain("Net Total");
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_ShowsExportButton()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        (await page.Locator("input[value='Download Excel Spreadsheet']").CountAsync()).Should().Be(1);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_CanExpandIncomeSection()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        await page.Locator("#incomeHeading button").ClickAsync();
        await page.WaitForSelectorAsync("#incomeDetails.show, #incomeDetails.collapse.show",
            new() { Timeout = 5000 });
        (await page.Locator("#incomeDetails button[type='submit']").CountAsync()).Should().Be(1);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_CanAddIncomeItem()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        await page.Locator("#incomeHeading button").ClickAsync();
        await page.WaitForSelectorAsync("#incomeDetails.show, #incomeDetails.collapse.show",
            new() { Timeout = 5000 });

        await page.Locator("#incomeDetails input[placeholder='xxx.xx']").FillAsync("1000");
        await page.Locator("#incomeDetails input[placeholder='how often?']").FillAsync("Weekly");
        await page.Locator("#incomeDetails input[placeholder='Income Description']").FillAsync("Test Salary");
        await page.Locator("#incomeDetails button[type='submit']").ClickAsync();

        await page.WaitForSelectorAsync("#incomeDetails table tr");
        (await page.Locator("#incomeDetails table").TextContentAsync()).Should().Contain("Test Salary");
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_ShowsPrivacyNotice()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        (await page.Locator("em").First.TextContentAsync())
            .Should().Contain("Nothing you put in here will be sent to our servers");
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_HasClearLink()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        (await page.Locator("a:has-text('clear')").CountAsync()).Should().Be(1);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task FootingMe_ShowsAllCategorySections()
    {
        SkipIfUnavailable();
        var page = await NavigateToFootingMe();
        foreach (var section in new[] { "income", "recurringBills", "householdBudgets", "personalBudgets", "eventBudgets" })
            (await page.Locator($"#{section}Heading").CountAsync()).Should().Be(1, $"section {section} should exist");
        await page.CloseAsync();
    }
}
