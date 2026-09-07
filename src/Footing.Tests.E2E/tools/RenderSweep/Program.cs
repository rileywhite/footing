using System.Text;
using System.Text.Json;
using Footing.Tests.E2E;
using Microsoft.Playwright;

namespace Footing.Tests.E2E.Tools.RenderSweep;

/// <summary>
/// W-16's render sweep and independent verification pass.
///
/// Two jobs, deliberately in one tool so they see the same published build:
///
///   1. THE SWEEP (BR-30, AC-15). 2 pages x 4 viewports x 2 colour schemes = 16 full-page
///      PNGs, written to .gc/artifacts/e2e-responsive-a11y/renders/. They exist for a human
///      to look at. Nothing asserts on them and they are never committed, which is what keeps
///      this clear of OOS-01's ban on golden images. .gc/ is gitignored.
///
///   2. THE INDEPENDENT CHECK. W-06, W-11 and W-15 each closed on numeric claims measured by
///      their own render evidence, which is gone -- it was scratch, by policy. This re-measures
///      those claims from scratch against the current published build and writes the result to
///      renders/verification.md, so "someone looked at it" is a real step with numbers behind
///      it rather than a restatement of what the closed items said.
///
/// Colour schemes follow D-04: BOTH runs emulate ColorScheme.Light and the dark run is driven
/// by the seeded `ft-theme` key alone. Emulating a dark OS preference as well would let
/// app.css's `@media (prefers-color-scheme: dark)` fallback supply the dark palette even if
/// the attribute route broke, and the renders would look right while showing the wrong
/// mechanism. Each run asserts nothing, but it does RECORD the computed body background, so a
/// dark render that is secretly light is visible in the report rather than only in the PNG.
/// </summary>
internal static class Program
{
    private sealed record Scheme(string Name, string? ThemeSeed);

    private static readonly Scheme Light = new("light", null);
    private static readonly Scheme Dark = new("dark", "dark");
    private static readonly Scheme[] Schemes = [Light, Dark];

    private static readonly Viewport[] AllViewports =
        [Viewports.MobileFloor, Viewports.Mobile, Viewports.Tablet, Viewports.Desktop];

    /// <summary>The description field in the entry form: the only bare `input` child of a `dd`.</summary>
    private const string DescriptionInput = "#moneyFlows dl > dd > input";

    private static async Task<int> Main()
    {
        // The sweep is worthless if it silently renders a "Loading..." placeholder because
        // Chromium is missing, so it runs under the strict half of the PLAYWRIGHT_REQUIRED
        // contract (BR-05, TS-11): a missing browser or publish output throws here.
        Environment.SetEnvironmentVariable("PLAYWRIGHT_REQUIRED", "1");

        // The fixture's WebApplication logs every static-file request at Information, which
        // for a WASM boot is several hundred lines per page and buries this tool's own
        // output. Set through the environment rather than by touching PlaywrightFixture:
        // OOS-08 says extend it, do not restructure it.
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
        Environment.SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore", "Warning");

        var outputDir = Path.Combine(FindRepoRoot(), ".gc", "artifacts", "e2e-responsive-a11y", "renders");
        Directory.CreateDirectory(outputDir);

        var fixture = new PlaywrightFixture();
        Console.WriteLine("Publishing Footing.Client (Release) and starting the static host...");
        await fixture.InitializeAsync();
        if (!fixture.ServerAvailable)
            throw new InvalidOperationException("The fixture reported the server unavailable.");
        Console.WriteLine($"Serving {fixture.BaseUrl}");

        var report = new StringBuilder();
        try
        {
            var rendered = await RenderSweepAsync(fixture, outputDir, report);
            var states = await RenderStatesAsync(fixture, outputDir, report);
            Console.WriteLine($"  ({states} supplementary state renders, outside the 16)");
            await VerifyW06Async(fixture, report);
            await VerifyW11Async(fixture, report);
            await VerifyW15Async(fixture, report);
            await CheckStickyBarOcclusionAsync(fixture, report);
            Console.WriteLine($"\n{rendered} renders written to {outputDir}");
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        var reportPath = Path.Combine(outputDir, "verification.md");
        await File.WriteAllTextAsync(reportPath, report.ToString());
        Console.WriteLine($"Verification report written to {reportPath}");
        return 0;
    }

    // ================================================================================
    // 1. The sweep
    // ================================================================================

    private static async Task<int> RenderSweepAsync(
        PlaywrightFixture fixture, string outputDir, StringBuilder report)
    {
        report.AppendLine("# W-16 render sweep and independent verification");
        report.AppendLine();
        report.AppendLine($"Generated {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz} against a fresh Release publish.");
        report.AppendLine("Scratch under `.gc/` (gitignored). Nothing here is asserted on or committed (OOS-01).");
        report.AppendLine();
        report.AppendLine("## 1. The 16 renders");
        report.AppendLine();
        report.AppendLine("Each page in the state a visitor lands in: the landing page as served, and the tool");
        report.AppendLine("page's first-time-user tree, which opens the Income card by default and so renders the");
        report.AppendLine("entry form W-06 repaired.");
        report.AppendLine();
        report.AppendLine("| file | page | viewport | scheme | body background | doc overflow |");
        report.AppendLine("|---|---|---|---|---|---|");

        var count = 0;
        foreach (var (pageName, path) in new[] { ("landing", SitePage.Landing), ("tool", SitePage.Tool) })
        {
            foreach (var viewport in AllViewports)
            {
                foreach (var scheme in Schemes)
                {
                    var seed = SeedFor(pageName, scheme);
                    await using var session = await fixture.NewSessionAsync(
                        viewport, ColorScheme.Light, localStorageSeed: seed);

                    await SitePage.GotoRenderedAsync(session.Page, fixture.BaseUrl, path);
                    if (pageName == "tool")
                        await session.Page.WaitForSelectorAsync(
                            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
                    await FreezeMotionAsync(session.Page);

                    var fileName = $"{pageName}-{viewport.Name}-{viewport.Width}x{viewport.Height}-{scheme.Name}.png";
                    await session.Page.ScreenshotAsync(new()
                    {
                        Path = Path.Combine(outputDir, fileName),
                        FullPage = true,
                    });
                    count++;

                    var background = await session.Page.EvaluateAsync<string>(
                        "() => getComputedStyle(document.body).backgroundColor");
                    var overflow = await OverflowAsync(session.Page);
                    report.AppendLine(
                        $"| `{fileName}` | {path} | {viewport} | {scheme.Name} | `{background}` | {overflow:0.##}px |");
                    Console.WriteLine($"  rendered {fileName}  bg={background} overflow={overflow:0.##}px");
                }
            }
        }

        report.AppendLine();
        report.AppendLine($"**{count} renders.** Per D-04 both runs emulate a light OS preference; the dark rows "
                        + "reach dark solely through the seeded `ft-theme` key, so a light background in a `dark` "
                        + "row would mean the attribute route broke.");
        report.AppendLine();
        return count;
    }

    /// <summary>
    /// The 16 sweep renders show each page in the state a visitor actually lands in, so the
    /// tool page carries NO analysis seed: that is the first-time-user tree, which opens the
    /// Income card by default and is therefore the only landing state that renders the entry
    /// form `dl` -- the element W-06 repaired. Seeding a returning user instead would produce
    /// sixteen renders in which the repaired element does not appear at all.
    ///
    /// The returning-user tree is covered by <see cref="RenderStatesAsync"/> below, outside
    /// the 16, because it is a state the user has to reach rather than a page.
    ///
    /// Dark is reached by seeding `ft-theme` on top, per D-04.
    /// </summary>
    private static Dictionary<string, string>? SeedFor(string pageName, Scheme scheme)
    {
        _ = pageName;
        return scheme.ThemeSeed is null
            ? null
            : ToolStorage.ThemeOnly(scheme.ThemeSeed);
    }

    /// <summary>
    /// Supplementary renders, deliberately NOT part of the 16 and counted separately.
    ///
    /// The sweep's contract is two pages; these are two tool-page STATES that a page-level
    /// sweep cannot reach, and both were touched by items this pass is checking: the
    /// returning-user tree collapsed (five entry chips, W-11's delete button) and expanded
    /// (the shared entry form at the two widths where it used to overflow, W-06). Only the
    /// two narrow viewports, because the wide layouts were never in question.
    /// </summary>
    private static async Task<int> RenderStatesAsync(
        PlaywrightFixture fixture, string outputDir, StringBuilder report)
    {
        var statesDir = Path.Combine(outputDir, "states");
        Directory.CreateDirectory(statesDir);

        report.AppendLine("### Supplementary state renders (not part of the 16)");
        report.AppendLine();
        report.AppendLine("Under `renders/states/`. The returning-user tree is a state, not a page, so it sits");
        report.AppendLine("outside the sweep's 2-pages contract -- but it is where W-11's entry-chip delete lives");
        report.AppendLine("and, expanded, where W-06's repaired entry form lives.");
        report.AppendLine();
        report.AppendLine("| file | state | viewport | scheme |");
        report.AppendLine("|---|---|---|---|");

        var count = 0;
        foreach (var expanded in new[] { false, true })
        {
            foreach (var viewport in new[] { Viewports.MobileFloor, Viewports.Mobile })
            {
                foreach (var scheme in Schemes)
                {
                    var seed = ToolStorage.ReturningUserWithEveryCategory();
                    if (scheme.ThemeSeed is not null) seed[ToolStorage.ThemeKey] = scheme.ThemeSeed;

                    await using var session = await fixture.NewSessionAsync(
                        viewport, ColorScheme.Light, localStorageSeed: seed);
                    await SitePage.GotoRenderedAsync(session.Page, fixture.BaseUrl, SitePage.Tool);
                    await session.Page.WaitForSelectorAsync(
                        "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });

                    if (expanded)
                    {
                        await session.Page.Locator("#incomeHeading button").ClickAsync();
                        await session.Page.WaitForSelectorAsync(
                            "#incomeDetails", new() { Timeout = 5000, State = WaitForSelectorState.Attached });
                    }

                    await FreezeMotionAsync(session.Page);

                    var state = expanded ? "returning-expanded" : "returning-collapsed";
                    var fileName = $"tool-{state}-{viewport.Width}-{scheme.Name}.png";
                    await session.Page.ScreenshotAsync(new()
                    {
                        Path = Path.Combine(statesDir, fileName),
                        FullPage = true,
                    });
                    count++;

                    report.AppendLine($"| `states/{fileName}` | {state} | {viewport} | {scheme.Name} |");
                    Console.WriteLine($"  rendered states/{fileName}");
                }
            }
        }

        report.AppendLine();
        report.AppendLine($"**{count} supplementary renders.**");
        report.AppendLine();
        return count;
    }

    // ================================================================================
    // 2a. W-06's claim: zero overflow at 320/375 in both tool-page trees; Tablet and
    //     Desktop untouched; tracks 167px 55px at 320 and 167px 110px at 375.
    // ================================================================================

    private static async Task VerifyW06Async(PlaywrightFixture fixture, StringBuilder report)
    {
        report.AppendLine("## 2. W-06 (`6ba8819`) re-measured");
        report.AppendLine();
        report.AppendLine("Claimed: overflow 0 at 320 and 375 in BOTH tool-page trees; Tablet and Desktop");
        report.AppendLine("unchanged (tracks `167px 439px`, description input 327px); tracks `167px 55px` at 320");
        report.AppendLine("and `167px 110px` at 375.");
        report.AppendLine();
        report.AppendLine("| tree | viewport | scheme | overflow | grid tracks | description input |");
        report.AppendLine("|---|---|---|---|---|---|");

        foreach (var tree in new[] { "first-run", "returning-expanded" })
        {
            foreach (var viewport in AllViewports)
            {
                foreach (var scheme in Schemes)
                {
                    var returning = tree == "returning-expanded";
                    var seed = returning ? ToolStorage.ReturningUserWithEveryCategory() : new Dictionary<string, string>();
                    if (scheme.ThemeSeed is not null) seed[ToolStorage.ThemeKey] = scheme.ThemeSeed;

                    await using var session = await fixture.NewSessionAsync(
                        viewport, ColorScheme.Light, localStorageSeed: seed.Count > 0 ? seed : null);
                    await SitePage.GotoRenderedAsync(session.Page, fixture.BaseUrl, SitePage.Tool);
                    await session.Page.WaitForSelectorAsync(
                        "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });

                    if (returning)
                    {
                        await session.Page.Locator("#incomeHeading button").ClickAsync();
                        await session.Page.WaitForSelectorAsync(
                            "#incomeDetails", new() { Timeout = 5000, State = WaitForSelectorState.Attached });
                    }

                    await FreezeMotionAsync(session.Page);

                    var overflow = await OverflowAsync(session.Page);
                    var tracks = await session.Page.EvalOnSelectorAsync<string>(
                        "#moneyFlows dl", "el => getComputedStyle(el).gridTemplateColumns");
                    var description = await session.Page.EvalOnSelectorAsync<double>(
                        DescriptionInput, "el => el.getBoundingClientRect().width");

                    report.AppendLine(
                        $"| {tree} | {viewport} | {scheme.Name} | {overflow:0.##}px | `{tracks}` | {description:0.#}px |");
                    Console.WriteLine($"  W-06 {tree} {viewport} {scheme.Name}: overflow={overflow:0.##} tracks={tracks} desc={description:0.#}");
                }
            }
        }
        report.AppendLine();
    }

    // ================================================================================
    // 2b. W-11's claim: the entry-chip delete and the net-total toggle are real buttons,
    //     keyboard reachable, with a visible focus indicator.
    // ================================================================================

    private static async Task VerifyW11Async(PlaywrightFixture fixture, StringBuilder report)
    {
        report.AppendLine("## 3. W-11 (`1b3c9f5`) re-measured");
        report.AppendLine();
        report.AppendLine("Claimed: the entry-chip delete and the sticky total are now real buttons, keyboard");
        report.AppendLine("reachable with visible focus. The net-total toggle landed on `.ft-sticky-total__summary`,");
        report.AppendLine("not on `.ft-sticky-total` itself (see F-05).");
        report.AppendLine();
        report.AppendLine("| control | tag | reachable by Tab | indicator changes on focus | what changed |");
        report.AppendLine("|---|---|---|---|---|");

        // A tree with entries, income expanded, so an entry chip exists to inspect.
        await using var session = await fixture.NewSessionAsync(
            Viewports.Desktop, ColorScheme.Light,
            localStorageSeed: ToolStorage.ReturningUserWithEveryCategory());
        await SitePage.GotoRenderedAsync(session.Page, fixture.BaseUrl, SitePage.Tool);
        await session.Page.WaitForSelectorAsync(
            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
        await session.Page.Locator("#incomeHeading button").ClickAsync();
        await session.Page.WaitForSelectorAsync(
            "#incomeDetails", new() { Timeout = 5000, State = WaitForSelectorState.Attached });
        await FreezeMotionAsync(session.Page);

        foreach (var (label, selector) in new[]
                 {
                     ("entry-chip delete", ".ft-entry-chip__delete"),
                     ("net-total toggle", ".ft-sticky-total__summary"),
                 })
        {
            var tag = await session.Page.EvalOnSelectorAsync<string>(selector, "el => el.tagName");

            await session.Page.EvaluateAsync("() => document.activeElement && document.activeElement.blur()");
            var before = await IndicatorAsync(session.Page, selector);
            var reachable = await TabToAsync(session.Page, selector);
            var after = await IndicatorAsync(session.Page, selector);

            report.AppendLine(
                $"| `{selector}` | `{tag}` | {(reachable ? "yes" : "**NO**")} | "
              + $"{(before != after ? "yes" : "**NO**")} | `{before}` -> `{after}` |");
            Console.WriteLine($"  W-11 {label}: tag={tag} reachable={reachable} indicator {before} -> {after}");
        }
        report.AppendLine();
    }

    /// <summary>
    /// Walks focus with REAL Tab presses until it lands on <paramref name="selector"/>, and
    /// reports whether it ever did.
    ///
    /// `Locator.FocusAsync()` is not usable here, and the first run of this tool proved it:
    /// programmatic focus does not put Chromium into keyboard modality, so `:focus-visible`
    /// never matches and both of W-11's repaired controls came back reporting NO indicator
    /// change -- an artifact of the measurement, not a defect in the repair. `KeyboardTests`
    /// already records this under D-08; this is the same rule, obeyed here too.
    /// </summary>
    private static async Task<bool> TabToAsync(IPage page, string selector, int maxPresses = 60)
    {
        for (var i = 0; i < maxPresses; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            var onTarget = await page.EvaluateAsync<bool>(
                "sel => document.activeElement !== null && document.activeElement.matches(sel)",
                selector);
            if (onTarget) return true;
        }
        return false;
    }

    /// <summary>
    /// D-11/RD-02's rule, narrowed to the control itself: the sweep is a check on W-11's two
    /// repaired controls, both of which draw their own UA `:focus-visible` ring, so the
    /// ancestor walk the suite performs is not needed to resolve them here.
    /// </summary>
    private static Task<string> IndicatorAsync(IPage page, string selector) =>
        page.EvalOnSelectorAsync<string>(
            selector,
            "el => { const s = getComputedStyle(el); "
          + "return [s.outlineStyle, s.outlineWidth, s.outlineColor, s.boxShadow, s.borderColor, s.backgroundColor].join(' | '); }");

    // ================================================================================
    // 2c. W-15's claim: the two provable accessibility defects are repaired -- F-03's
    //     heading-level skip on the tool page and F-04's three unlabelled controls.
    // ================================================================================

    private static async Task VerifyW15Async(PlaywrightFixture fixture, StringBuilder report)
    {
        report.AppendLine("## 4. W-15 (`da2837f`) re-measured");
        report.AppendLine();
        report.AppendLine("Claimed: two provable accessibility defects repaired -- F-03 (the tool page skipped");
        report.AppendLine("heading levels) and F-04 (three entry-form controls had no programmatic label).");
        report.AppendLine();
        report.AppendLine("### Heading outline (F-03)");
        report.AppendLine();
        report.AppendLine("| page | state | levels in document order | skip? |");
        report.AppendLine("|---|---|---|---|");

        foreach (var (pageName, path) in new[] { ("landing", SitePage.Landing), ("tool", SitePage.Tool) })
        {
            await using var session = await fixture.NewSessionAsync(
                Viewports.Desktop, ColorScheme.Light,
                localStorageSeed: pageName == "tool" ? ToolStorage.ReturningUserWithEveryCategory() : null);
            await SitePage.GotoRenderedAsync(session.Page, fixture.BaseUrl, path);
            if (pageName == "tool")
                await session.Page.WaitForSelectorAsync(
                    "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });

            // Collapsed first, then with the net-total bar expanded. F-03 records why the
            // second state is not optional: the sticky detail panel carries a heading of its
            // own and is absent from the DOM until the bar is open, so a check that never
            // expands it would miss a skip RELOCATED into that panel rather than removed.
            await ReportHeadingsAsync(session.Page, path, "collapsed", report);
            if (pageName == "tool")
            {
                await session.Page.Locator(".ft-sticky-total__summary").First.ClickAsync();
                await session.Page.WaitForSelectorAsync(
                    "#totalDetail", new() { Timeout = 5000, State = WaitForSelectorState.Attached });
                await ReportHeadingsAsync(session.Page, path, "net-total expanded", report);
            }
        }

        report.AppendLine();
        report.AppendLine("### Entry-form accessible names (F-04)");
        report.AppendLine();
        report.AppendLine("| control | accessible name |");
        report.AppendLine("|---|---|");

        await using (var session = await fixture.NewSessionAsync(Viewports.Desktop))
        {
            await SitePage.GotoRenderedAsync(session.Page, fixture.BaseUrl, SitePage.Tool);
            await session.Page.WaitForSelectorAsync(
                "#moneyFlows dl", new() { Timeout = 60000, State = WaitForSelectorState.Attached });

            foreach (var (label, selector) in new[]
                     {
                         ("money amount", "#moneyFlows .ft-input-money__field"),
                         ("period select", "#moneyFlows .ft-period-select"),
                         ("description", DescriptionInput),
                     })
            {
                // getAttribute rather than the full accname algorithm: W-15's repair was
                // specifically aria-label, and naming the mechanism is what makes a
                // regression legible here.
                var name = await session.Page.EvalOnSelectorAsync<string?>(
                    selector, "el => el.getAttribute('aria-label')");
                report.AppendLine($"| {label} (`{selector}`) | {(string.IsNullOrWhiteSpace(name) ? "**none**" : $"`{name}`")} |");
                Console.WriteLine($"  W-15 {label}: aria-label={name ?? "(none)"}");
            }
        }
        report.AppendLine();
    }

    private static async Task ReportHeadingsAsync(
        IPage page, string path, string state, StringBuilder report)
    {
        var levels = await page.EvaluateAsync<int[]>(
            "() => [...document.querySelectorAll('h1,h2,h3,h4,h5,h6')].map(h => +h.tagName[1])");
        var skip = false;
        for (var i = 1; i < levels.Length; i++)
            if (levels[i] - levels[i - 1] > 1) skip = true;

        report.AppendLine($"| {path} | {state} | {string.Join(", ", levels)} | {(skip ? "**YES**" : "no")} |");
        Console.WriteLine($"  W-15 {path} ({state}) headings: {string.Join(",", levels)} skip={skip}");
    }

    // ================================================================================
    // 2d. Something the sweep itself raised: `.ft-sticky-total` is `position: fixed;
    //     bottom: 0` (app.css:797), so in a FULL-PAGE capture Chromium leaves it at the
    //     original viewport's bottom edge and it appears to sit in the middle of the
    //     document, overlapping a card. That is a screenshot artifact and nothing else --
    //     but it is worth asking the question it prompts, which no test in the suite asks:
    //     scrolled to the very bottom, does the bar cover the last controls on the page?
    // ================================================================================

    private static async Task CheckStickyBarOcclusionAsync(PlaywrightFixture fixture, StringBuilder report)
    {
        report.AppendLine("## 5. Does the fixed net-total bar cover anything at the page bottom?");
        report.AppendLine();
        report.AppendLine("`.ft-sticky-total` is `position: fixed; bottom: 0` (app.css:797). In the full-page");
        report.AppendLine("renders above it therefore appears mid-document, overlapping a card -- that is a");
        report.AppendLine("Chromium full-page capture artifact, not a layout defect, and the Desktop renders");
        report.AppendLine("(where the page fits the viewport) show it correctly pinned to the bottom.");
        report.AppendLine();
        report.AppendLine("The question the artifact prompts is real, though, and no test in the suite asks it:");
        report.AppendLine("scrolled to the very bottom, is the last control on the page behind the bar?");
        report.AppendLine();
        report.AppendLine("| viewport | tree | last control | its bottom | bar top | clear? |");
        report.AppendLine("|---|---|---|---|---|---|");

        foreach (var viewport in AllViewports)
        {
            foreach (var returning in new[] { false, true })
            {
                await using var session = await fixture.NewSessionAsync(
                    viewport, ColorScheme.Light,
                    localStorageSeed: returning ? ToolStorage.ReturningUserWithEveryCategory() : null);
                await SitePage.GotoRenderedAsync(session.Page, fixture.BaseUrl, SitePage.Tool);
                await session.Page.WaitForSelectorAsync(
                    "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
                await FreezeMotionAsync(session.Page);

                await session.Page.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)");
                await session.Page.WaitForFunctionAsync(
                    "() => Math.abs(window.scrollY + window.innerHeight - document.documentElement.scrollHeight) < 2 "
                  + "|| document.documentElement.scrollHeight <= window.innerHeight");

                // The bottom-most control that is not the bar itself or inside it.
                //
                // Returned as a JSON STRING, not as an object. F-13 records why, and this tool
                // walked straight into it on its first run: Playwright .NET cannot deserialize
                // a JS object into Dictionary<string, string> -- it does not throw, it hands
                // back an EMPTY dictionary, and the first indexer call is where you find out.
                var measuredJson = await session.Page.EvaluateAsync<string?>(
                    """
                    () => {
                      const bar = document.querySelector('.ft-sticky-total');
                      if (!bar) return null;
                      const controls = [...document.querySelectorAll('a, button, input, select, textarea')]
                        .filter(el => !bar.contains(el))
                        .map(el => ({ el, r: el.getBoundingClientRect() }))
                        .filter(x => x.r.width > 0 && x.r.height > 0);
                      if (controls.length === 0) return null;
                      const last = controls.reduce((a, b) => (b.r.bottom > a.r.bottom ? b : a));
                      const barRect = bar.getBoundingClientRect();
                      return JSON.stringify({
                        // `value` before the tag name: <input type="button"> is named by its
                        // value attribute (HTML-AAM) and has no textContent, so without it the
                        // export button reports as a bare "INPUT" and reads like a missing label.
                        label: (last.el.textContent || last.el.getAttribute('aria-label') || last.el.value || last.el.tagName).trim().slice(0, 40),
                        bottom: Math.round(last.r.bottom),
                        barTop: Math.round(barRect.top),
                      });
                    }
                    """);

                if (measuredJson is null)
                {
                    report.AppendLine($"| {viewport} | {(returning ? "returning" : "first-run")} | (none found) | | | |");
                    continue;
                }

                using var measured = JsonDocument.Parse(measuredJson);
                var label = measured.RootElement.GetProperty("label").GetString();
                var bottom = measured.RootElement.GetProperty("bottom").GetDouble();
                var barTop = measured.RootElement.GetProperty("barTop").GetDouble();
                var clear = bottom <= barTop;
                report.AppendLine(
                    $"| {viewport} | {(returning ? "returning" : "first-run")} | `{label}` | "
                  + $"{bottom}px | {barTop}px | {(clear ? "yes" : "**NO, overlapped**")} |");
                Console.WriteLine(
                    $"  sticky {viewport} {(returning ? "returning" : "first-run")}: "
                  + $"last='{label}' bottom={bottom} barTop={barTop} clear={clear}");
            }
        }
        report.AppendLine();
    }

    // ================================================================================
    // Shared helpers
    // ================================================================================

    /// <summary>
    /// The same freeze F-16 records `ContrastTests` needing, for the same reason: app.css
    /// animates `background-color` on `.ft-sticky-total`, `border-color`/`box-shadow` on the
    /// form controls, and `.ft-entry-chip` has an entrance animation, so `getComputedStyle`
    /// (and a screenshot) can catch an interpolated mid-transition value. Without this the
    /// renders are not reproducible and neither are the numbers beside them.
    /// </summary>
    private static async Task FreezeMotionAsync(IPage page)
    {
        await page.AddStyleTagAsync(new()
        {
            Content = "*, *::before, *::after { transition: none !important; animation: none !important; }",
        });
        await page.EvaluateAsync("() => document.activeElement && document.activeElement.blur()");
    }

    private static Task<double> OverflowAsync(IPage page) =>
        page.EvaluateAsync<double>(
            "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find the repository root (no .git above the tool's output directory).");
    }
}
