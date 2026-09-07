using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// BR-26: WCAG 2.1 AA text contrast, in BOTH colour schemes, on both pages.
///
/// The site does not currently meet AA. That is not this suite's to fix: every failing pair
/// below is a `:root` palette token (`--ft-accent`, `--ft-text-muted`, `--ft-primary`,
/// `--ft-primary-hover`, and white-on-`--ft-primary`/`--ft-amount-negative` in dark), and
/// changing a brand colour is a redesign under D-10, which belongs to Riley. Full detail is in
/// the findings ledger as F-16.
///
/// So these tests PIN the known failures rather than asserting zero, for the same reason
/// F-12's overflow is pinned: CR-01 means a permanently red assertion here blocks every merge
/// on a protected branch, and skipping would forfeit AC-01's no-skips promise. What is pinned
/// is deliberately NOT the violation count or the element selectors -- it is the set of
/// distinct FOREGROUND/BACKGROUND COLOUR PAIRS. The defect is a palette defect, so the colour
/// pairs are the finding; selectors churn with markup and counts churn with content, while a
/// colour pair changes only when the palette does. Concretely, 60 violation nodes across the
/// eight runs reduce to 7 distinct pairs in light and 2 in dark.
///
/// The pin cuts both ways, which is the point:
///   * a NEW colour pair fails -- that is a real regression, and the ongoing value here;
///   * a pair that stops failing also fails, so a palette fix cannot leave this baseline
///     silently stale. When Riley fixes the palette, delete the pairs that now pass.
/// </summary>
[Collection("Playwright")]
public class ContrastTests
{
    private readonly PlaywrightFixture _fixture;
    public ContrastTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    /// <summary>A failing foreground/background pair, with the palette token behind it.</summary>
    private sealed record ColourPair(string Foreground, string Background)
    {
        public override string ToString() => $"fg={Foreground} on bg={Background}";
    }

    /// <summary>
    /// Known-failing pairs in the LIGHT scheme, measured at AA (4.5:1 for body text, 3:1 for
    /// large). Ratios are recorded in the comments rather than asserted: they are a
    /// deterministic function of the two colours, so pinning the pair pins the ratio, and
    /// pinning a formatted decimal as well would only add a way to break on rounding.
    /// </summary>
    private static readonly Dictionary<ColourPair, string> KnownLightFailures = new()
    {
        [new("#b89e78", "#f6f2ec")] = "2.29 -- --ft-accent on --ft-bg",
        [new("#b89e78", "#ffffff")] = "2.56 -- --ft-accent on a white surface",
        [new("#6e7d72", "#ede8e0")] = "3.55 -- --ft-text-muted on --ft-bg-topbar",
        [new("#5a7a6a", "#ede8e0")] = "3.89 -- --ft-primary on --ft-bg-topbar",
        [new("#6e7d72", "#f6f2ec")] = "3.89 -- --ft-text-muted on --ft-bg",
        [new("#4d6b5c", "#dfd8cc")] = "4.15 -- --ft-primary-hover on --ft-bg-topbar-border",
        [new("#6e7d72", "#ffffff")] = "4.33 -- --ft-text-muted on a white surface",
    };

    /// <summary>
    /// Known-failing pairs in the DARK scheme. Both are white text on a saturated fill -- the
    /// net-total bar, which is `--ft-primary` when positive and `--ft-amount-negative` when
    /// negative. Dark fails far less than light because dark's `--ft-text-muted` (#9a9690) is
    /// light enough against its backgrounds; light's (#6e7d72) is not.
    /// </summary>
    private static readonly Dictionary<ColourPair, string> KnownDarkFailures = new()
    {
        [new("#ffffff", "#7da893")] = "2.66 -- white on --ft-primary (the positive net-total bar)",
        [new("#ffffff", "#e06050")] = "3.52 -- white on --ft-amount-negative (the negative bar)",
    };

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
        HashSet<ColourPair> FailingPairs,
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

        var failing = new HashSet<ColourPair>();
        foreach (var node in result.Violations.SelectMany(violation => violation.Nodes))
        {
            var pair = ParseColourPair(node);
            if (pair is not null)
            {
                failing.Add(pair);
            }
        }

        // Violations and incompletes are kept apart all the way through, never merged into one
        // "problems" list: an incomplete is axe declining to judge, and D-09 says that is
        // reported, not asserted.
        var incompleteNodes = result.Incomplete.SelectMany(item => item.Nodes).ToList();

        return new ScanResult(
            bodyBackground,
            themeAttribute,
            failing,
            incompleteNodes.SelectMany(n => n.Any.Select(check => check.Message ?? "")).Distinct().ToList(),
            incompleteNodes.Select(n => Squash(n.Html)).ToList());
    }

    /// <summary>
    /// Pulls the two colours out of axe's own message, which is the only place the resolved
    /// pair appears in a stable form -- the check's `Data` is an untyped object over the wire.
    /// Returns null rather than throwing if the shape ever changes; a pair that cannot be read
    /// is then simply not in the observed set, which surfaces as a MISSING known pair (a loud,
    /// named failure) rather than as a silently empty result that passes.
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

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Contrast_MatchesTheKnownPaletteBaseline(bool dark)
    {
        SkipIfUnavailable();

        var scheme = dark ? "dark" : "light";
        var known = dark ? KnownDarkFailures : KnownLightFailures;

        var observed = new HashSet<ColourPair>();
        var unknownByContext = new List<string>();
        var unexpectedIncompletes = new List<string>();

        foreach (var (label, path, toolState) in Contexts)
        {
            var scan = await ScanAsync(path, toolState, dark);

            scan.ThemeAttribute.Should().Be(
                dark ? "dark" : null,
                $"{label} ({scheme}): the scheme must be driven, not inherited (D-04)");

            observed.UnionWith(scan.FailingPairs);

            foreach (var pair in scan.FailingPairs.Where(pair => !known.ContainsKey(pair)))
            {
                unknownByContext.Add($"{label}: {pair}");
            }

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

        unknownByContext.Should().BeEmpty(
            $"no contrast failure outside the known {scheme} palette baseline is allowed -- a new "
            + "foreground/background pair here is a real regression. The baseline is a palette "
            + "problem recorded as F-16 for Riley (D-10 redesign), not something this suite fixes; "
            + $"unknown pairs: {string.Join("; ", unknownByContext)}");

        unexpectedIncompletes.Should().BeEmpty(
            "the only contrast incompletes today are decorative glyphs with no text content; a "
            + "different incomplete reason means axe could not resolve a background (D-09) and "
            + $"belongs in the findings ledger: {string.Join("; ", unexpectedIncompletes)}");

        // The other half of the pin: if a known failure stops failing, the palette was fixed and
        // this baseline is stale. That is good news, and it still has to fail here so the
        // baseline is updated rather than quietly over-reporting for ever.
        var repaired = known.Keys.Where(pair => !observed.Contains(pair))
            .Select(pair => $"{pair} ({known[pair]})")
            .ToList();

        repaired.Should().BeEmpty(
            $"every known {scheme} contrast failure should still be present, or this baseline is "
            + "stale. If the palette was deliberately fixed, DELETE these entries from "
            + $"Known{(dark ? "Dark" : "Light")}Failures and update F-16: {string.Join("; ", repaired)}");
    }
}
