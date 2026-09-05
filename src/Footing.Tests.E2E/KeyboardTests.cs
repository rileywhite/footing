using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// BR-19/BR-20: every interactive control on both pages is reachable by Tab and shows a
/// visible focus indicator.
/// </summary>
[Collection("Playwright")]
public class KeyboardTests
{
    private readonly PlaywrightFixture _fixture;
    public KeyboardTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    private sealed class Candidate
    {
        public string Probe { get; set; } = "";
        public string Selector { get; set; } = "";
    }

    public static IEnumerable<object[]> BothPages =>
        new[] { SitePage.Landing, SitePage.Tool }.Select(page => new object[] { page });

    /// <summary>
    /// The generic ARIA-shaped query, unioned with an explicit allowlist.
    ///
    /// R-02 — the allowlist is load-bearing, not belt-and-braces. `.ft-sticky-total` is a
    /// plain &lt;div @onclick="ToggleStickyExpand"&gt; with no role and no tabindex
    /// (FootingAnalysisEditor.razor:80 and :171, duplicated across both trees). It is
    /// invisible to the generic query PRECISELY BECAUSE it is undeclared -- which is the
    /// defect. Without this allowlist the enumeration would report "expected set covered",
    /// this item would pass, and the failing test W-11's repair is gated on would never
    /// appear; proven-first discipline would then block the fix it exists to justify.
    ///
    /// The allowlist is a standing maintenance cost: every future undeclared control has to
    /// be remembered here or it goes unchecked. The right long-term fix is F-05's repair
    /// making the control self-describing (a real button, or role + tabindex), after which
    /// it falls out of the generic query naturally and this entry can be deleted.
    ///
    /// `.ft-entry-chip__delete` is deliberately NOT listed: it is a &lt;span role="button"&gt;,
    /// so `[role="button"]` already matches it. It is non-focusable for want of a tabindex,
    /// which the reachability assertion catches on its own.
    /// </summary>
    private const string EnumerateCandidates = """
        () => {
            const GENERIC = 'a[href], button, input:not([type=hidden]), select, textarea, '
                + '[tabindex]:not([tabindex="-1"]), [role="button"]';
            const ALLOWLIST = ['.ft-sticky-total'];

            const describe = el => {
                const id = el.id ? '#' + el.id : '';
                const cls = Array.from(el.classList).map(c => '.' + c).join('');
                return el.tagName.toLowerCase() + id + cls;
            };

            const found = new Set(document.querySelectorAll(GENERIC));
            for (const selector of ALLOWLIST) {
                for (const el of document.querySelectorAll(selector)) found.add(el);
            }

            const visible = Array.from(found).filter(el => {
                const r = el.getBoundingClientRect();
                if (r.width <= 0 || r.height <= 0) return false;
                const cs = getComputedStyle(el);
                return cs.visibility !== 'hidden' && cs.display !== 'none';
            });

            visible.forEach((el, i) => el.setAttribute('data-kbd-probe', String(i)));
            return visible.map((el, i) => ({ probe: String(i), selector: describe(el) }));
        }
        """;

    /// <summary>
    /// D-11 (Riley's decision RD-02: fix the assertion, not the CSS).
    ///
    /// Returns a per-level style signature for the element AND its ancestor chain, up to and
    /// including main/body. Two things make this shape necessary:
    ///
    ///  1. ANCESTORS COUNT. `.ft-input-money__field` declares
    ///     `border: none !important; box-shadow: none !important; outline: none`
    ///     (app.css:1057-1065). Those !important declarations outrank the unimportant
    ///     box-shadow in `.btn:focus` / `.form-control:focus`, so when the money input is
    ///     focused its OWN computed outline and box-shadow are byte-identical to unfocused --
    ///     while the user sees a perfectly good ring drawn on the wrapper by
    ///     `.ft-input-money:focus-within` (app.css:1042). Asserting on the control alone would
    ///     fail a control that is not broken, and D-10 classifies restyling focus rings as a
    ///     redesign, so nothing downstream would have the authority to make it green.
    ///     :focus-within on a wrapper is a legitimate, common pattern -- an indicator found on
    ///     an ancestor is a PASS.
    ///
    ///  2. MORE THAN OUTLINE AND BOX-SHADOW. `.ft-period-select:focus` (app.css:1079) indicates
    ///     focus via border-color plus box-shadow with `outline: none !important`, and
    ///     `.ft-input-money:focus-within` uses border-color too. background-color is included
    ///     for the same reason -- `.form-control:focus` sets it.
    ///
    /// Do not "simplify" this back to comparing outline and box-shadow on the control itself.
    /// It was written this way on purpose, and the reasons are in the stylesheet above.
    ///
    /// A control with no indicator anywhere in its chain is written to the findings ledger for
    /// Riley. It is NOT restyled here.
    ///
    /// (An aside that looks like a third case but is not: `.btn-accordian-header:focus`
    /// (app.css:527) sets outline: none and box-shadow: none -- but `.btn:focus` (app.css:533)
    /// has equal specificity and comes later in source order, so the ring wins on the accordion
    /// header. Exactly the cascade subtlety that makes the wider, ancestor-aware comparison the
    /// safe one.)
    /// </summary>
    private const string ReadChainSignature = """
        probe => {
            const PROPS = ['outline-width', 'outline-style', 'outline-color', 'box-shadow',
                'border-top-color', 'border-right-color', 'border-bottom-color',
                'border-left-color', 'background-color'];
            const el = document.querySelector('[data-kbd-probe="' + probe + '"]');
            if (!el) return [];
            const levels = [];
            let node = el;
            while (node && node !== document.documentElement) {
                const cs = getComputedStyle(node);
                levels.push(PROPS.map(p => cs.getPropertyValue(p)).join('|'));
                if (node.tagName === 'MAIN' || node.tagName === 'BODY') break;
                node = node.parentElement;
            }
            return levels;
        }
        """;

    private const string ReadActiveProbe =
        "() => document.activeElement ? document.activeElement.getAttribute('data-kbd-probe') : null";

    private const string DescribeActive = """
        () => {
            const el = document.activeElement;
            if (!el) return '(none)';
            const id = el.id ? '#' + el.id : '';
            const cls = Array.from(el.classList).map(c => '.' + c).join('');
            return el.tagName.toLowerCase() + id + cls;
        }
        """;

    /// <summary>
    /// Opens a page in the state that actually contains the controls under test. On the tool
    /// page that means a seeded returning user with the income section expanded: the entry
    /// chips (and their delete affordance) only exist when there are entries, and every
    /// MoneyFlowCard renders collapsed until clicked.
    ///
    /// Transitions are frozen before any style is read. `.form-control` declares
    /// `transition: border-color 0.15s, box-shadow 0.15s` and `.ft-period-select`
    /// `transition: border-color 0.2s`, and getComputedStyle returns the INTERPOLATED value
    /// mid-transition -- so a focus indicator could read as absent purely because the
    /// measurement won the race. Frozen rather than slept past, since this lands on the
    /// protected gate (CR-01).
    /// </summary>
    private async Task<PageSession> OpenAsync(string path)
    {
        var session = await _fixture.NewSessionAsync(
            Viewports.Desktop,
            localStorageSeed: path == SitePage.Tool ? ToolStorage.ReturningUser() : null);

        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, path);

        if (path == SitePage.Tool)
        {
            await session.Page.WaitForSelectorAsync(
                "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
            await session.Page.Locator("#incomeHeading button").ClickAsync();
            await session.Page.WaitForSelectorAsync("#incomeDetails .ft-entry-chip", new() { Timeout = 15000 });
        }

        await session.Page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = "*, *::before, *::after { transition: none !important; animation: none !important; }",
        });

        // Leave the page with nothing focused. Clicking #incomeHeading above focuses that
        // button, and the unfocused baseline in the focus-indicator test is taken from this
        // state -- without the blur, that one control's "unfocused" reading already contains
        // its focus ring, and it is then reported as having no indicator. It has one; the
        // setup was standing on it. (Verified: the other four accordion headers, which are
        // never clicked, resolve their ring correctly via .btn:focus winning source order
        // over .btn-accordian-header:focus.)
        await session.Page.EvaluateAsync(
            "() => document.activeElement instanceof HTMLElement && document.activeElement.blur()");

        return session;
    }

    /// <summary>
    /// D-08: walks with REAL Tab presses. Locator.FocusAsync() does not reliably set
    /// :focus-visible, and the UA focus ring on plain links depends on it, so a
    /// focus()-driven walk would both miss reachability defects and misreport indicators.
    /// Bounded: it stops once focus returns to the first control it reached, and in any case
    /// after a hard cap, so a page that never cycles cannot hang the gate.
    /// </summary>
    private static async Task<(HashSet<string> Probes, List<string> Order)> TabWalkAsync(
        IPage page, int candidateCount)
    {
        var reached = new HashSet<string>();
        var order = new List<string>();
        string? firstDescriptor = null;
        var cap = (candidateCount * 3) + 40;

        for (var i = 0; i < cap; i++)
        {
            await page.Keyboard.PressAsync("Tab");

            var descriptor = await page.EvaluateAsync<string>(DescribeActive);
            var probe = await page.EvaluateAsync<string?>(ReadActiveProbe);

            if (probe is not null)
            {
                reached.Add(probe);
            }
            order.Add(descriptor);

            // Cycle detection keys on the probe id where there is one, falling back to the
            // descriptor. Descriptors are not unique -- the five collapsed accordion headers
            // render identical class lists -- so keying on the descriptor alone could stop the
            // walk a whole lap early if focus happened to start on one of them.
            var key = probe ?? descriptor;
            if (firstDescriptor is null)
            {
                firstDescriptor = key;
            }
            else if (key == firstDescriptor && i > 0)
            {
                break;
            }
        }

        return (reached, order);
    }

    [SkippableTheory]
    [MemberData(nameof(BothPages))]
    public async Task AllInteractiveControls_AreReachableByTab(string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(path);
        var page = session.Page;

        var candidates = await page.EvaluateAsync<Candidate[]>(EnumerateCandidates);
        candidates.Should().NotBeEmpty($"{path} should have interactive controls to check");

        var (reached, order) = await TabWalkAsync(page, candidates.Length);

        var unreachable = candidates
            .Where(candidate => !reached.Contains(candidate.Probe))
            .Select(candidate => candidate.Selector)
            .ToList();

        unreachable.Should().BeEmpty(
            $"every interactive control on {path} must be reachable by Tab; "
            + $"unreachable: {string.Join(", ", unreachable)}; "
            + $"tab order was: {string.Join(" -> ", order)}");
    }

    [SkippableTheory]
    [MemberData(nameof(BothPages))]
    public async Task EveryReachableControl_ShowsAFocusIndicator(string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(path);
        var page = session.Page;

        var candidates = await page.EvaluateAsync<Candidate[]>(EnumerateCandidates);
        var byProbe = candidates.ToDictionary(c => c.Probe, c => c.Selector);

        // Unfocused baseline for the whole chain of every candidate, taken while nothing on
        // the page has focus.
        var unfocused = new Dictionary<string, string[]>();
        foreach (var candidate in candidates)
        {
            unfocused[candidate.Probe] = await page.EvaluateAsync<string[]>(ReadChainSignature, candidate.Probe);
        }

        var withoutIndicator = new List<string>();
        var checkedCount = 0;

        var (reached, _) = await TabWalkAsync(page, candidates.Length);

        // Re-walk, measuring at each stop. Measuring during the first walk would work too, but
        // the baseline above has to be taken with nothing focused, and re-walking keeps the two
        // phases from interleaving.
        var seen = new HashSet<string>();
        var cap = (candidates.Length * 3) + 40;
        await page.EvaluateAsync("() => document.activeElement && document.activeElement.blur()");

        for (var i = 0; i < cap && seen.Count < reached.Count; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            var probe = await page.EvaluateAsync<string?>(ReadActiveProbe);
            if (probe is null || !seen.Add(probe))
            {
                continue;
            }

            var focused = await page.EvaluateAsync<string[]>(ReadChainSignature, probe);
            var baseline = unfocused[probe];

            var moved = focused.Length == baseline.Length
                && focused.Where((level, index) => level != baseline[index]).Any();

            checkedCount++;
            if (!moved)
            {
                withoutIndicator.Add(byProbe[probe]);
            }
        }

        checkedCount.Should().BeGreaterThan(0, $"{path} should have focusable controls to measure");
        withoutIndicator.Should().BeEmpty(
            $"every reachable control on {path} must show a focus indicator on itself or an "
            + $"ancestor (D-11); without one: {string.Join(", ", withoutIndicator)}");
    }

    // The two D-11 regression cases called out explicitly in the item: if either of these
    // fails, the ancestor-and-wider-property rule was not implemented and the general
    // assertion above is measuring the wrong thing.
    [SkippableTheory]
    [InlineData(".ft-input-money__field", "resolves via .ft-input-money:focus-within on the wrapper")]
    [InlineData(".ft-period-select", "resolves on itself, via border-color rather than outline")]
    public async Task FocusIndicator_Resolves_ForTheTwoD11Cases(string selector, string how)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(SitePage.Tool);
        var page = session.Page;

        await page.EvaluateAsync<Candidate[]>(EnumerateCandidates);
        var probe = await page.EvalOnSelectorAsync<string>(selector, "el => el.getAttribute('data-kbd-probe')");
        probe.Should().NotBeNull($"'{selector}' should have been enumerated as a candidate");

        var baseline = await page.EvaluateAsync<string[]>(ReadChainSignature, probe);
        await page.Locator(selector).First.FocusAsync();
        var focused = await page.EvaluateAsync<string[]>(ReadChainSignature, probe);

        focused.Should().HaveCount(baseline.Length);
        focused.Where((level, index) => level != baseline[index]).Should().NotBeEmpty(
            $"'{selector}' must resolve a focus indicator -- {how} (D-11)");
    }
}
