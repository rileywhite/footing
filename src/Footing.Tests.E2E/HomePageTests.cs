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

    [SkippableFact]
    public async Task HomePage_LoadsSuccessfully()
    {
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        var response = await page.GotoAsync(_fixture.BaseUrl);
        response!.Status.Should().Be(200);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task HomePage_HasFootingMeLink()
    {
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        var links = page.Locator("a[href='footing-me']");
        await links.First.WaitForAsync();
        (await links.CountAsync()).Should().BeGreaterThan(0);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task HomePage_HasControlYourMoneyLink()
    {
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        var links = page.Locator("a[href='control-your-money']");
        await links.First.WaitForAsync();
        (await links.CountAsync()).Should().BeGreaterThan(0);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task HomePage_NavigatesToFootingMe()
    {
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        await page.Locator("a[href='footing-me']").First.ClickAsync();
        await page.WaitForURLAsync("**/footing-me");
        page.Url.Should().Contain("footing-me");
        await page.CloseAsync();
    }
}
