using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// BR-13/BR-14/BR-15: the theme toggle flips the attribute, actually changes what is
/// rendered, persists the choice, and survives a reload -- on both pages.
///
/// footing.js owns the behaviour: applyTheme reads localStorage['ft-theme'] and sets
/// data-theme on &lt;html&gt;; toggleTheme falls back to matchMedia('(prefers-color-scheme: dark)')
/// when no attribute is set, then writes the flipped value back. Both pages inline a copy of
/// the restore snippet in &lt;head&gt;, so the reload path exercises that snippet rather than any
/// Blazor code -- which is why these run on the landing page too, where there is no app at all.
/// </summary>
[Collection("Playwright")]
public class ThemeToggleTests
{
    private const string ToggleSelector = "button.theme-toggle";

    private readonly PlaywrightFixture _fixture;
    public ThemeToggleTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    // Pages, not viewports: the theme is viewport-invariant -- app.css has no width-based
    // media queries at all, and nothing in footing.js consults width -- so running this
    // across the matrix would multiply gate time (CR-01) for no new information. Desktop is
    // the named single viewport.
    public static IEnumerable<object[]> BothPages =>
        new[] { SitePage.Landing, SitePage.Tool }.Select(page => new object[] { page });

    // D-04: the colour scheme is always DRIVEN, never inherited. The unset-data-theme baseline
    // is ColorScheme.Light precisely because toggleTheme consults matchMedia when no attribute
    // is set -- on a runner whose OS prefers dark, an inherited scheme would make the very
    // first toggle flip to light and every assertion below read backwards.
    private async Task<PageSession> OpenAsync(
        string path, ColorScheme colorScheme, IDictionary<string, string>? seed = null)
    {
        var session = await _fixture.NewSessionAsync(
            Viewports.Desktop, colorScheme: colorScheme, localStorageSeed: seed);
        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, path);
        return session;
    }

    private static Task<string?> ReadThemeAttributeAsync(IPage page) =>
        page.EvaluateAsync<string?>("() => document.documentElement.getAttribute('data-theme')");

    private static Task<string> ReadBodyBackgroundAsync(IPage page) =>
        page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");

    private static Task<string?> ReadStoredThemeAsync(IPage page) =>
        page.EvaluateAsync<string?>("key => localStorage.getItem(key)", ToolStorage.ThemeKey);

    /// <summary>
    /// Waits for data-theme to reach <paramref name="expected"/>. A bounded browser-side poll
    /// rather than a sleep: toggleTheme is synchronous, so this almost always returns on the
    /// first evaluation, but a fixed delay would be both slower and less reliable on a loaded
    /// runner (CR-01).
    /// </summary>
    private static Task WaitForThemeAsync(IPage page, string expected) =>
        page.WaitForFunctionAsync(
            "expected => document.documentElement.getAttribute('data-theme') === expected",
            expected,
            new PageWaitForFunctionOptions { Timeout = 10000 });

    [SkippableTheory]
    [MemberData(nameof(BothPages))]
    public async Task ColdStart_TogglesToDark_AndSurvivesAReload(string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(path, ColorScheme.Light);
        var page = session.Page;

        (await ReadThemeAttributeAsync(page))
            .Should().BeNull($"{path} should start with no stored theme and no data-theme attribute");
        var lightBackground = await ReadBodyBackgroundAsync(page);

        await page.Locator(ToggleSelector).ClickAsync();
        await WaitForThemeAsync(page, "dark");

        var darkBackground = await ReadBodyBackgroundAsync(page);

        // The assertion that matters. A flipped attribute with an unchanged rendering is
        // exactly the regression class this whole effort exists to catch -- #62 shipped with
        // green CI because nothing checked that the markup still had its styling.
        darkBackground.Should().NotBe(
            lightBackground,
            $"toggling to dark on {path} must actually change what is rendered, not just set an "
            + "attribute (light was {0})", lightBackground);

        (await ReadStoredThemeAsync(page)).Should().Be("dark", "the choice must be persisted");

        await page.ReloadAsync();
        await page.Locator(SitePage.ContentSelector).WaitForAsync(new() { Timeout = 60000 });

        (await ReadThemeAttributeAsync(page))
            .Should().Be("dark", $"the <head> restore snippet on {path} must reapply the stored theme");
        (await ReadBodyBackgroundAsync(page))
            .Should().Be(darkBackground, "the reloaded page must render dark, not merely claim to");
    }

    [SkippableTheory]
    [MemberData(nameof(BothPages))]
    public async Task WarmStart_ComesUpDark_AndTogglesBackToLight(string path)
    {
        SkipIfUnavailable();

        // Seeded through the context, so ft-theme is in storage BEFORE the first navigation and
        // the <head> snippet applies it before first paint -- which is the path being tested.
        // An init script would re-run on every navigation and defeat the toggle below.
        await using var session = await OpenAsync(
            path,
            ColorScheme.Light,
            new Dictionary<string, string> { [ToolStorage.ThemeKey] = "dark" });
        var page = session.Page;

        (await ReadThemeAttributeAsync(page))
            .Should().Be("dark", $"a seeded ft-theme must be applied on {path} before first paint");
        var darkBackground = await ReadBodyBackgroundAsync(page);

        await page.Locator(ToggleSelector).ClickAsync();
        await WaitForThemeAsync(page, "light");

        var lightBackground = await ReadBodyBackgroundAsync(page);
        lightBackground.Should().NotBe(
            darkBackground,
            $"toggling back to light on {path} must actually change what is rendered (dark was {0})",
            darkBackground);

        (await ReadStoredThemeAsync(page)).Should().Be("light", "the reverse choice must persist too");

        await page.ReloadAsync();
        await page.Locator(SitePage.ContentSelector).WaitForAsync(new() { Timeout = 60000 });

        (await ReadThemeAttributeAsync(page))
            .Should().Be("light", "the restore snippet must reapply light, not fall back to the OS");
        (await ReadBodyBackgroundAsync(page))
            .Should().Be(lightBackground, "the reloaded page must render light");
    }
}
