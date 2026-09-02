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
        var links = page.Locator("a[href='find-my-footing/']");
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
        await page.Locator("a[href='find-my-footing/']").First.ClickAsync();
        await page.WaitForURLAsync("**/find-my-footing/");
        page.Url.Should().Contain("find-my-footing/");
        await page.CloseAsync();
    }

    [SkippableFact]
    public async Task ErrorUi_IsSingleAndHidden()
    {
        // "" is the static landing page now -- it has no Blazor app and no
        // #blazor-error-ui at all, so it's not part of this check anymore.
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/find-my-footing/");
        var errorUi = page.Locator("#blazor-error-ui");
        (await errorUi.CountAsync()).Should().Be(1);
        (await errorUi.EvaluateAsync<string>("el => getComputedStyle(el).display"))
            .Should().Be("none");
        await page.CloseAsync();
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

    [SkippableFact]
    public async Task PageLoad_RendersSelfHostedRubik()
    {
        SkipIfUnavailable();
        var page = await _fixture.Browser.NewPageAsync();
        var requestUrls = new List<string>();
        page.Request += (_, request) => requestUrls.Add(request.Url);

        await page.GotoAsync(_fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => true)");

        var rubikRequests = requestUrls
            .Where(url => url.Contains("rubik", StringComparison.OrdinalIgnoreCase)
                && url.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
            .ToList();

        rubikRequests.Should().NotBeEmpty(
            "the self-hosted Rubik woff2 must actually be fetched -- a font that silently fails to "
            + "load issues no request at all, which passes PageLoad_IssuesNoThirdPartyRequests vacuously");

        // Not getComputedStyle: that returns the declared stack whether or not Rubik ever loaded.
        // Not document.fonts.check(): it reports true even for a face the browser skipped.
        var faceLoaded = await page.EvaluateAsync<bool>(
            "() => Array.from(document.fonts).some(f => f.family.includes('Rubik') && f.status === 'loaded')");

        faceLoaded.Should().BeTrue("a Rubik @font-face must reach status 'loaded'");

        // An unsupported format() keyword makes the browser skip the src and fall back silently,
        // so compare against the same stack with Rubik removed.
        var widthDelta = await page.EvaluateAsync<double>("""
            () => {
                const probe = (family) => {
                    const el = document.createElement('div');
                    el.style.cssText = 'position:absolute;left:-9999px;top:-9999px;'
                        + 'white-space:nowrap;font-size:100px;font-family:' + family;
                    el.textContent = 'Handgloves 123';
                    document.body.appendChild(el);
                    const width = el.getBoundingClientRect().width;
                    el.remove();
                    return width;
                };
                const fallback = "-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
                return Math.abs(probe("'Rubik', " + fallback) - probe(fallback));
            }
            """);

        widthDelta.Should().BeGreaterThan(0.5,
            "text must render in Rubik, not in the fallback stack");

        await page.CloseAsync();
    }
}
