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

    [SkippableFact]
    public async Task ErrorUi_IsSingleAndHidden()
    {
        SkipIfUnavailable();
        foreach (var path in new[] { "", "find-my-footing" })
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync($"{_fixture.BaseUrl}/{path}");
            var errorUi = page.Locator("#blazor-error-ui");
            (await errorUi.CountAsync()).Should().Be(1);
            (await errorUi.EvaluateAsync<string>("el => getComputedStyle(el).display"))
                .Should().Be("none");
            await page.CloseAsync();
        }
    }

    [SkippableFact]
    public async Task PageLoad_IssuesNoThirdPartyRequests()
    {
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        var requestUrls = new List<string>();
        page.Request += (_, request) => requestUrls.Add(request.Url);

        await page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var baseOrigin = new Uri(_fixture.BaseUrl).GetLeftPart(UriPartial.Authority);
        var thirdParty = requestUrls
            .Where(url => url.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            .Where(url => new Uri(url).GetLeftPart(UriPartial.Authority) != baseOrigin)
            .ToList();

        thirdParty.Should().BeEmpty($"no third-party requests should be issued, but saw: {string.Join(", ", thirdParty)}");
        await page.CloseAsync();
    }
}
