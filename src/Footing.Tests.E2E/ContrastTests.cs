using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// BR-26: WCAG 2.1 AA text contrast, in BOTH colour schemes, on both pages.
///
/// This asserts AA outright -- zero `color-contrast` violations across the eight combinations
/// below. It did not always: F-16 recorded nine failing foreground/background pairs (seven in
/// light, two in dark) that were all `:root` palette tokens, and because changing a brand
/// colour is a redesign under D-10 this suite PINNED that set rather than asserting zero, so
/// CR-01 would not leave a protected branch permanently red. Riley authorised the palette
/// repair, the tokens moved, and the pin is gone: tolerating the old failures after they were
/// fixed would be strictly worse than asserting the real requirement.
///
/// What the repair changed, for anyone reading a future regression here: light's green ramp
/// and `--ft-text-muted` were darkened in place (hue and saturation held, lightness lowered);
/// `--ft-accent` kept its value as the `.btn-accent` fill and gained `--ft-accent-strong` for
/// its foreground-text role, because one tan cannot be both AA-legible on cream and light
/// enough to carry dark button text; and the hard-coded `#fff` on the brand fills became
/// `--ft-fill-text`, which is white in light and a dark ink in dark, because dark's
/// `--ft-primary` and `--ft-amount-negative` are chosen to be read AS text on a dark ground
/// and so cannot also sit under white.
/// </summary>
[Collection("Playwright")]
public class ContrastTests
{
    private readonly PlaywrightFixture _fixture;
    public ContrastTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    /// <summary>A foreground/background pair axe judged to fail, as it resolved them.</summary>
    private sealed record ColourPair(string Foreground, string Background)
    {
        public override string ToString() => $"fg={Foreground} on bg={Background}";
    }

    /// <summary>
    /// The page states scanned. Both pages, and all three tool-page states, because each
    /// renders elements the others do not: the first-time-user tree has the guiding sentences
    /// and the open entry form, the compact tree has the collapsed headers and "Switch to
    /// guided flow", and expanding a compact card adds the form on top of the compact tree.
    /// </summary>
    private static readonly (string Label, string Path, string? ToolState)[] Contexts =
    [
        ("landing", SitePage.Landing, null),
        ("tool/first-time-user", SitePage.Tool, null),
        ("tool/returning-user", SitePage.Tool, "returning"),
        ("tool/returning-user, income expanded", SitePage.Tool, "expanded"),
    ];

    private sealed record ScanResult(
        string BodyBackground,
        string? ThemeAttribute,
        List<string> Violations,
        List<string> IncompleteReasons,
        List<string> IncompleteHtml);

    /// <summary>
    /// Opens one page state in one colour scheme and runs axe's `color-contrast` rule.
    ///
    /// D-04 -- the scheme is DRIVEN, never inherited, and the dark run is driven by the seeded
    /// `ft-theme` key ALONE, with the emulated OS preference left at Light for both runs. That
    /// is deliberate: app.css reaches dark by two independent routes, `[data-theme="dark"]`
    /// and a `@media (prefers-color-scheme: dark)` fallback scoped to `:root:not([data-theme])`
    /// (app.css:110 and :163). Emulating a dark OS preference as well would let the media
    /// fallback silently supply the dark palette if the seed ever stopped working, and the
    /// scan would still look right while testing the wrong mechanism. With the preference held
    /// at Light, dark can only come from the attribute the &lt;head&gt; restore snippet sets.
    ///
    /// Transitions and animations are frozen before the scan. This is load-bearing for CR-01,
    /// not hygiene: axe reads computed colours, and `getComputedStyle` returns the INTERPOLATED
    /// value mid-transition. Measured unfrozen, the same page reported 13 violations on one run
    /// and 3 on the next, with blended near-miss colours (#df6150 where the palette says
    /// #e06050). Frozen, three consecutive runs of all eight combinations were identical.
    /// </summary>
    private async Task<ScanResult> ScanAsync(string path, string? toolState, bool dark)
    {
        var seed = new Dictionary<string, string>();
        if (dark)
        {
            seed[ToolStorage.ThemeKey] = "dark";
        }
        if (toolState is not null)
        {
            seed[ToolStorage.AnalysisKey] = ToolStorage.EntryInEveryCategory;
        }

        await using var session = await _fixture.NewSessionAsync(
            Viewports.Desktop,
            colorScheme: ColorScheme.Light,
            localStorageSeed: seed.Count > 0 ? seed : null);

        var page = session.Page;
        await SitePage.GotoRenderedAsync(page, _fixture.BaseUrl, path);

        if (path == SitePage.Tool)
        {
            await page.WaitForSelectorAsync(
                "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
            if (toolState == "expanded")
            {
                await page.Locator("#incomeHeading button").ClickAsync();
                await page.WaitForSelectorAsync("#incomeDetails", new() { Timeout = 15000 });
            }
        }

        await page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = "*, *::before, *::after { transition: none !important; animation: none !important; }",
        });
        // Clicking to expand leaves that control focused, and a focus ring changes computed
        // colours -- which would make the scan depend on the setup rather than the palette.
        await page.EvaluateAsync(
            "() => document.activeElement instanceof HTMLElement && document.activeElement.blur()");

        var bodyBackground = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.body).backgroundColor");
        var themeAttribute = await page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");

        var result = await page.RunAxe(new AxeRunOptions
        {
            RunOnly = RunOnlyOptions.Rules(new[] { "color-contrast" }),
            ResultTypes = [ResultType.Violations, ResultType.Incomplete],
        });

        // One entry per violating NODE rather than per distinct colour pair. The pair is the
        // useful part of the report -- it names the palette token -- but the assertion is on the
        // nodes, so a violation whose message axe reformats one day still fails loudly instead of
        // parsing to null and disappearing out of a deduplicated set.
        var violations = result.Violations
            .SelectMany(violation => violation.Nodes)
            .Select(node => $"{ParseColourPair(node)?.ToString() ?? "unparsed"} -- {Squash(node.Html)}")
            .Distinct()
            .ToList();

        // Violations and incompletes are kept apart all the way through, never merged into one
        // "problems" list: an incomplete is axe declining to judge, and D-09 says that is
        // reported, not asserted.
        var incompleteNodes = result.Incomplete.SelectMany(item => item.Nodes).ToList();

        return new ScanResult(
            bodyBackground,
            themeAttribute,
            violations,
            incompleteNodes.SelectMany(n => n.Any.Select(check => check.Message ?? "")).Distinct().ToList(),
            incompleteNodes.Select(n => Squash(n.Html)).ToList());
    }

    /// <summary>
    /// Pulls the two colours out of axe's own message, which is the only place the resolved
    /// pair appears in a stable form -- the check's `Data` is an untyped object over the wire.
    /// Returns null rather than throwing if the shape ever changes. Nothing is lost when it
    /// does: the assertion counts violation NODES, so an unreadable message still fails -- it
    /// just reports as `unparsed` alongside the offending element's HTML.
    /// </summary>
    private static ColourPair? ParseColourPair(AxeResultNode node)
    {
        foreach (var message in node.Any.Select(check => check.Message ?? ""))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                message,
                @"foreground color: (#[0-9a-fA-F]+), background color: (#[0-9a-fA-F]+)");
            if (match.Success)
            {
                return new ColourPair(match.Groups[1].Value.ToLowerInvariant(), match.Groups[2].Value.ToLowerInvariant());
            }
        }
        return null;
    }

    private static string Squash(string? value) =>
        System.Text.RegularExpressions.Regex.Replace(value ?? "", @"\s+", " ").Trim();

    /// <summary>
    /// The precondition the item requires be established BEFORE any contrast result is
    /// trusted: the dark run must actually render dark. Asserted on both pages, comparing a
    /// computed colour between the two runs rather than trusting the seed.
    /// </summary>
    [SkippableTheory]
    [InlineData(SitePage.Landing)]
    [InlineData(SitePage.Tool)]
    public async Task DarkRun_ActuallyRendersDark(string path)
    {
        SkipIfUnavailable();

        var light = await ScanAsync(path, null, dark: false);
        var dark = await ScanAsync(path, null, dark: true);

        light.ThemeAttribute.Should().BeNull(
            $"{path}: the light run must have no stored theme, so it exercises the default palette");
        dark.ThemeAttribute.Should().Be(
            "dark", $"{path}: the <head> restore snippet must apply the seeded ft-theme before first paint");

        dark.BodyBackground.Should().NotBe(
            light.BodyBackground,
            $"{path}: the two runs must render different palettes, or the dark contrast result "
            + $"is just the light one measured twice (light={light.BodyBackground}, dark={dark.BodyBackground})");
    }

    /// <summary>
    /// Zero `color-contrast` violations, in one scheme, across all four page states.
    ///
    /// Failures are collected across every context before asserting rather than asserted per
    /// context, so a palette change that breaks three states reports all three in one run
    /// instead of hiding two behind the first failure.
    /// </summary>
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Contrast_MeetsAa(bool dark)
    {
        SkipIfUnavailable();

        var scheme = dark ? "dark" : "light";
        var violations = new List<string>();
        var unexpectedIncompletes = new List<string>();

        foreach (var (label, path, toolState) in Contexts)
        {
            var scan = await ScanAsync(path, toolState, dark);

            scan.ThemeAttribute.Should().Be(
                dark ? "dark" : null,
                $"{label} ({scheme}): the scheme must be driven, not inherited (D-04)");

            violations.AddRange(scan.Violations.Select(violation => $"{label}: {violation}"));

            // D-09: incompletes are reported, not asserted -- but a NEW KIND of incomplete is
            // worth knowing about, so the REASON is pinned even though the finding is not. The
            // only reason seen today is decorative glyphs (the ▶/▼/▲ chevrons), which axe
            // declines to judge because they are not text. If an "unable to determine the
            // background colour" incomplete ever appears -- a gradient, an image, translucency,
            // exactly the case D-09 was written for -- this names it instead of swallowing it.
            foreach (var reason in scan.IncompleteReasons.Where(
                reason => !reason.Contains("non-text characters", StringComparison.OrdinalIgnoreCase)))
            {
                unexpectedIncompletes.Add($"{label}: {reason}");
            }
        }

        violations.Should().BeEmpty(
            $"every text/background pair the {scheme} palette produces must meet WCAG AA (4.5:1 "
            + "for body text, 3:1 for large text and UI components). Each failure below names the "
            + "colours axe resolved; both are almost always :root tokens in app.css, so fix them "
            + $"there rather than at the call site: {string.Join("; ", violations)}");

        unexpectedIncompletes.Should().BeEmpty(
            "the only contrast incompletes today are decorative glyphs with no text content; a "
            + "different incomplete reason means axe could not resolve a background (D-09) and "
            + $"belongs in the findings ledger: {string.Join("; ", unexpectedIncompletes)}");
    }
}
