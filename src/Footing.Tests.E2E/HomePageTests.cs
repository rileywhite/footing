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
    public async Task HomePage_HasFindMyFootingLink()
    {
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        var links = page.Locator("a[href='find-my-footing']");
        await links.First.WaitForAsync();
        (await links.CountAsync()).Should().BeGreaterThan(0);
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task HomePage_NavigatesToFindMyFooting()
    {
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        await page.Locator("a[href='find-my-footing']").First.ClickAsync();
        await page.WaitForURLAsync("**/find-my-footing");
        page.Url.Should().Contain("find-my-footing");
        await page.CloseAsync();
    }
}
