using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// BR-21..BR-25: the structural accessibility invariants, on both pages, at every full
/// viewport, using the engine W-12 selected.
///
/// Three of these were failing when W-13 wrote them, and the tests were written to CATCH them
/// rather than around them. All three have since been repaired, so their pins are gone and
/// the invariants are asserted positively. F-01 was the last one out: its landing half was
/// repaired first and its tool half was PINNED at zero in the meantime rather than asserted
/// away or skipped, because CR-01 means a permanently red assertion blocks every merge on a
/// protected branch and AC-01 forbids skipping. A pin fails in both directions -- a new
/// violation is a regression, and a violation that disappears means the baseline is stale and
/// must be updated by whoever repaired it. Both halves have now landed and the pin is gone,
/// which is the pin working as designed rather than being worked around.
///
///   * F-01 -- `contentinfo`. FULLY REPAIRED, and asserted unconditionally: both pages have
///     exactly one.
///     The LANDING page's `footer.ft-landing-footer` used to sit inside `article.content`
///     inside `main`, where a `footer` element is generic rather than a landmark, and W-15
///     reported rather than repaired it because promoting the footer out of `main` costs it
///     `main`'s 54rem cap and 1rem gutter -- the `border-top` rule would span the viewport
///     (832px -> 1280px at desktop) and the block would drop 64px past `.content`'s bottom
///     padding. Riley authorised the promotion together with the CSS that gives the footer
///     that box back, so the move is now visually neutral to the pixel; `LandingFooterTests`
///     pins the geometry that says so, and this asserts the landmark it bought.
///     The TOOL page had no footer element at all, and this suite pinned that zero because
///     giving it one is new UI and so Riley's call under D-10. Riley made that call on
///     2026-09-07: the tool page now carries `footer.ft-tool-footer` with the privacy line
///     the landing page had all along ("Everything here stays in your browser"), which is the
///     actual point of the change -- the landmark is the free side effect of building it
///     correctly, since no WCAG success criterion requires a `contentinfo` at all.
///     `ToolFooterTests` pins that footer's geometry the way `LandingFooterTests` pins the
///     landing one's.
///   * F-03 -- REPAIRED by W-15. The tool page went `h1` straight to the `h5` card headers.
///     The card headers are now `h2` and the sticky net-total detail heading with them, so
///     both pages descend without a skip and this asserts that positively. The LANDING page
///     never skipped (h1 then h2), which is worth knowing before anyone "fixes" it.
///   * F-04 -- REPAIRED by W-15. The three entry-form controls in `MoneyFlowCard.razor` now
///     carry an `aria-label` naming which card they belong to; the placeholders stayed.
///
/// A NOTE ON WHY BR-23 IS HAND-WRITTEN AND NOT DELEGATED TO AXE. **Do not collapse
/// FormControls_HaveProgrammaticLabels onto axe's `label` rule now that both pass.** Before
/// W-15's repair, axe's `label` rule PASSED on the tool page while two of the three controls
/// had no label at all: `placeholder` genuinely does contribute to the accessible name as a
/// last resort, so by axe's reckoning they were named. BR-23 is deliberately stricter -- "a
/// `dt` prompt sitting next to an input is not a label", and neither is a placeholder, which
/// vanishes as soon as the user types. Delegating BR-23 to axe would have reported the tool
/// page as passing and two thirds of F-04 would never have surfaced. The two rules agree today
/// only because the real labels are there; delete the hand-written one and the next control
/// that ships with nothing but a placeholder passes silently.
/// </summary>
[Collection("Playwright")]
public class StructuralAccessibilityTests
{
    private readonly PlaywrightFixture _fixture;
    public StructuralAccessibilityTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    /// <summary>
    /// Viewport x page. The tool page is opened in the returning-user state with a card
    /// expanded, which is the state that renders the most structure at once: all five card
    /// headers (BR-22), the entry form (BR-23), and an entry chip with its delete control
    /// (BR-24). The first-time-user tree is a strict subset for every assertion here.
    /// </summary>
    public static IEnumerable<object[]> FullViewportsByPage =>
        from viewport in new[] { Viewports.Mobile, Viewports.Tablet, Viewports.Desktop }
        from page in new[] { SitePage.Landing, SitePage.Tool }
        select new object[] { viewport, page };

    private async Task<PageSession> OpenAsync(Viewport viewport, string path)
    {
        var session = await _fixture.NewSessionAsync(
            viewport,
            localStorageSeed: path == SitePage.Tool ? ToolStorage.ReturningUserWithEveryCategory() : null);

        await SitePage.GotoRenderedAsync(session.Page, _fixture.BaseUrl, path);

        if (path == SitePage.Tool)
        {
            await session.Page.WaitForSelectorAsync(
                "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });
            await session.Page.Locator("#incomeHeading button").ClickAsync();
            await session.Page.WaitForSelectorAsync("#incomeDetails", new() { Timeout = 15000 });
            await session.Page.EvaluateAsync(
                "() => document.activeElement instanceof HTMLElement && document.activeElement.blur()");
        }

        return session;
    }

    // ================================================================================
    // BR-21 -- landmarks
    // ================================================================================

    /// <summary>
    /// Counts landmark roles as an assistive technology would resolve them.
    ///
    /// `header` and `footer` expose banner/contentinfo ONLY at the top level of the document.
    /// Nested inside main/article/section/aside/nav they are generic, which was the whole of
    /// F-01 on the landing page: the footer element existed and looked right in the markup,
    /// and was not a landmark. A naive `document.querySelector('footer')` reported it present
    /// throughout. That nesting rule is still what this has to enforce -- the landing footer
    /// is a landmark today only because it is a sibling of `main`, and dropping it back
    /// inside `main` would restore the defect without removing a single element.
    /// </summary>
    private const string CountLandmarks = """
        () => {
            const NATIVE = { header: 'banner', nav: 'navigation', main: 'main', footer: 'contentinfo', aside: 'complementary' };
            const counts = {};
            const bump = role => { if (role) counts[role] = (counts[role] || 0) + 1; };
            for (const el of document.querySelectorAll('header, nav, main, footer, aside, [role]')) {
                const tag = el.tagName.toLowerCase();
                const explicit = el.getAttribute('role');
                if (explicit) { bump(explicit); continue; }
                const native = NATIVE[tag];
                if (!native) continue;
                if ((tag === 'header' || tag === 'footer') && el.closest('main, article, section, aside, nav')) continue;
                bump(native);
            }
            return Object.entries(counts).map(([role, n]) => `${role}=${n}`);
        }
        """;

    [SkippableTheory]
    [MemberData(nameof(FullViewportsByPage))]
    public async Task Landmarks_ArePresentAndUnique(Viewport viewport, string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(viewport, path);

        var counts = (await session.Page.EvaluateAsync<string[]>(CountLandmarks))
            .Select(entry => entry.Split('='))
            .ToDictionary(parts => parts[0], parts => int.Parse(parts[1]));

        var where = $"{path} at {viewport}";
        counts.GetValueOrDefault("banner").Should().Be(1, $"{where} should have exactly one banner landmark");
        counts.GetValueOrDefault("navigation").Should().Be(1, $"{where} should have exactly one navigation landmark");
        counts.GetValueOrDefault("main").Should().Be(1, $"{where} should have exactly one main landmark");

        // F-01, and no longer per page. This used to branch: the landing page asserted 1 and
        // the tool page PINNED 0, because the tool page had no footer element and adding one
        // was new UI that only Riley could authorise. Riley authorised it on 2026-09-07, both
        // pages now carry a footer that is a sibling of main, and the branch is gone rather
        // than left standing with both arms saying 1.
        counts.GetValueOrDefault("contentinfo").Should().Be(
            1,
            $"{where}: F-01 -- the page's footer is a sibling of main and must expose exactly "
            + "one contentinfo landmark. IF THIS FAILS AT ZERO, footer.ft-landing-footer "
            + "(index.html) or footer.ft-tool-footer (MainLayout.razor) has been moved back "
            + "inside main/article, where a footer element is generic rather than a landmark "
            + "-- the element being present in the markup is not enough");
    }

    // ================================================================================
    // BR-22 -- heading order
    // ================================================================================

    /// <summary>
    /// The heading levels in document order, as `h1`/`h5`/... including anything with an
    /// explicit aria-level.
    /// </summary>
    private const string ReadHeadingLevels = """
        () => Array.from(document.querySelectorAll('h1,h2,h3,h4,h5,h6,[role="heading"]'))
            .filter(el => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0; })
            .map(el => {
                const explicit = el.getAttribute('aria-level');
                const level = explicit ? Number(explicit) : Number(el.tagName.slice(1));
                const text = (el.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 40);
                return `${level}|${text}`;
            })
        """;

    [SkippableTheory]
    [MemberData(nameof(FullViewportsByPage))]
    public async Task HeadingOrder_HasExactlyOneH1_AndNoSkippedLevels(Viewport viewport, string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(viewport, path);

        var headings = (await session.Page.EvaluateAsync<string[]>(ReadHeadingLevels))
            .Select(entry => entry.Split('|', 2))
            .Select(parts => (Level: int.Parse(parts[0]), Text: parts[1]))
            .ToList();

        var where = $"{path} at {viewport}";

        headings.Should().NotBeEmpty($"{where} should have headings");
        headings.Count(h => h.Level == 1).Should().Be(1, $"{where} should have exactly one h1");
        headings[0].Level.Should().Be(1, $"{where}: the first heading should be the h1");

        var skips = new List<string>();
        for (var i = 1; i < headings.Count; i++)
        {
            if (headings[i].Level > headings[i - 1].Level + 1)
            {
                skips.Add($"h{headings[i - 1].Level} \"{headings[i - 1].Text}\" -> h{headings[i].Level} \"{headings[i].Text}\"");
            }
        }

        // F-03, REPAIRED by W-15 and now asserted positively on BOTH pages. The landing page
        // was always correct (h1 then h2); the tool page went h1 straight to the h5 card
        // headers, and MoneyFlowCard now renders them as h2. Asserted for the landing page too
        // rather than assumed, because "the site skips heading levels" is the natural way to
        // summarise F-03 when only one page ever did, and a repair aimed at both would have
        // changed a page that was already right.
        skips.Should().BeEmpty(
            $"{where} should not skip heading levels; found: {string.Join("; ", skips)}");

        if (path != SitePage.Tool)
        {
            return;
        }

        // The sticky net-total detail panel is the other half of the F-03 repair, and it is not
        // in the DOM until the bar is expanded -- it renders only under `@if (stickyExpanded)`
        // -- so nothing above reaches it. Its heading holds visible text of its own, unlike the
        // card headers, so it is the one that could regress unnoticed: leaving it at h5 while
        // the cards moved to h2 would have RELOCATED the skip rather than removed it, and every
        // assertion above would still have passed. (Verified by doing exactly that: the h5
        // reappears in the message below, naming the pair, at all three tool viewports.)
        //
        // The panel renders one of TWO headings depending on the sign of the net total, and
        // this seed is net-negative, so it is "Time to roll up your sleeves" that gets checked
        // here. Both are written at the same level in FootingAnalysisEditor.razor; if they ever
        // diverge, this reaches only one of them.
        await session.Page.Locator("#totalHeading").ClickAsync();
        await session.Page.WaitForSelectorAsync("#totalDetail", new() { Timeout = 15000 });
        await session.Page.EvaluateAsync(
            "() => document.activeElement instanceof HTMLElement && document.activeElement.blur()");

        var expanded = (await session.Page.EvaluateAsync<string[]>(ReadHeadingLevels))
            .Select(entry => entry.Split('|', 2))
            .Select(parts => (Level: int.Parse(parts[0]), Text: parts[1]))
            .ToList();

        expanded.Should().HaveCountGreaterThan(
            headings.Count,
            $"{where}: expanding the net-total bar should reveal its detail heading, or this is "
            + "just checking the collapsed page a second time");

        var expandedSkips = new List<string>();
        for (var i = 1; i < expanded.Count; i++)
        {
            if (expanded[i].Level > expanded[i - 1].Level + 1)
            {
                expandedSkips.Add(
                    $"h{expanded[i - 1].Level} \"{expanded[i - 1].Text}\" -> h{expanded[i].Level} \"{expanded[i].Text}\"");
            }
        }

        expandedSkips.Should().BeEmpty(
            $"{where}: the net-total detail heading must descend from the h2 card headers above "
            + $"it; found: {string.Join("; ", expandedSkips)}");
    }

    // ================================================================================
    // BR-23 -- form labels
    // ================================================================================

    /// <summary>
    /// Visible user-input controls that have NO programmatic label.
    ///
    /// `placeholder` is deliberately NOT accepted as a label -- see the class comment. Buttons
    /// are excluded because their name comes from their content, which is BR-24's concern.
    /// </summary>
    private const string FindUnlabelledControls = """
        () => Array.from(document.querySelectorAll(
                'input:not([type=hidden]):not([type=button]):not([type=submit]):not([type=reset]), select, textarea'))
            .filter(el => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0; })
            .filter(el => {
                if (el.getAttribute('aria-label')) return false;
                if (el.getAttribute('aria-labelledby')) return false;
                if (el.getAttribute('title')) return false;
                if (el.id && document.querySelector(`label[for="${CSS.escape(el.id)}"]`)) return false;
                if (el.closest('label')) return false;
                return true;   // NOTE: a placeholder does not rescue it. That is the point.
            })
            .map(el => {
                const cls = Array.from(el.classList).filter(c => c !== 'valid' && c !== 'modified' && c !== 'invalid');
                const placeholder = el.getAttribute('placeholder');
                return el.tagName.toLowerCase()
                    + (cls.length ? '.' + cls.join('.') : '')
                    + (placeholder ? ` [placeholder="${placeholder}"]` : ' [no placeholder]');
            })
        """;

    [SkippableTheory]
    [MemberData(nameof(FullViewportsByPage))]
    public async Task FormControls_HaveProgrammaticLabels(Viewport viewport, string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(viewport, path);

        var unlabelled = await session.Page.EvaluateAsync<string[]>(FindUnlabelledControls);
        var where = $"{path} at {viewport}";

        // F-04, REPAIRED by W-15 and now asserted as empty on BOTH pages. The landing page has
        // no form controls at all; the tool page's three entry-form controls in
        // MoneyFlowCard.razor each carry a real aria-label. A placeholder is still not accepted
        // as a label by the probe above -- see the class comment on why this check is
        // hand-written rather than delegated to axe's `label` rule, which passed all the way
        // through the defect.
        unlabelled.Should().BeEmpty(
            $"{where}: every visible input, select and textarea needs a programmatic label -- "
            + "aria-label, aria-labelledby, title, a label[for] or a wrapping <label>. A "
            + "placeholder is not one: it is not exposed as a label to every assistive "
            + $"technology and it vanishes as soon as the user types. Found: {string.Join("; ", unlabelled)}");

        if (path != SitePage.Tool)
        {
            return;
        }

        // The names themselves, not merely their presence. Five identical entry forms can sit
        // on this page at once, so a bare "Amount" three times over would satisfy the check
        // above and still leave a screen-reader user unable to tell the cards apart. The income
        // card is the one this fixture expands.
        var names = await session.Page.EvaluateAsync<string[]>(
            // InputText renders a bare <input> with no type attribute, so `input[type=text]`
            // would match nothing -- the description field is "the input that is not the money
            // field".
            "() => ['#incomeDetails .ft-input-money__field', '#incomeDetails .ft-period-select', "
            + "'#incomeDetails input:not(.ft-input-money__field)'].map(sel => { "
            + "const el = document.querySelector(sel); "
            + "return el ? (el.getAttribute('aria-label') || '(no aria-label)') : '(missing)'; })");

        names.Should().Equal(
            ["Income amount", "Income frequency", "Income description"],
            $"{where}: each entry-form control must name the card it belongs to, because five "
            + "identical forms share this page");
    }

    // ================================================================================
    // BR-24 -- accessible names on icon-only controls
    // ================================================================================

    /// <summary>
    /// The two controls whose entire visible content is an icon or glyph, so their accessible
    /// name can only come from an aria-label. Asserted by exact value rather than merely
    /// "has a name": BR-24 says assert the labels rather than trusting them, and a label that
    /// silently changed to something unhelpful would still be "present".
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(FullViewportsByPage))]
    public async Task IconOnlyControls_HaveAccessibleNames(Viewport viewport, string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(viewport, path);
        var where = $"{path} at {viewport}";

        // The theme toggle is in the shared chrome, so it is on both pages. Its visible content
        // is two aria-hidden glyphs, leaving nothing but the aria-label.
        var toggle = session.Page.Locator("button.theme-toggle");
        (await toggle.CountAsync()).Should().Be(1, $"{where} should have the theme toggle");
        (await toggle.GetAttributeAsync("aria-label")).Should().Be(
            "Toggle dark mode", $"{where}: the theme toggle's only accessible name is its aria-label");

        if (path == SitePage.Landing)
        {
            return;
        }

        // The chip delete is an empty <button> whose × is drawn by `.bi-x::before`, i.e. CSS
        // generated content -- so it has no text content at all and the aria-label is the whole
        // of its accessible name. Seeded via ToolStorage, so the expected name is known.
        var delete = session.Page.Locator(".ft-entry-chip__delete").First;
        (await delete.CountAsync()).Should().BeGreaterThan(0, $"{where} should render an entry chip to delete");
        (await delete.TextContentAsync()).Should().BeEmpty(
            $"{where}: the delete control's × comes from .bi-x::before, so it has no text to name it");
        (await delete.GetAttributeAsync("aria-label")).Should().Be(
            $"Remove {ToolStorage.SeededEntryName}",
            $"{where}: the delete control must name WHICH entry it removes -- a bare \"Remove\" "
            + "is ambiguous when several chips are listed");
    }

    // ================================================================================
    // BR-25 -- alt text
    // ================================================================================

    /// <summary>
    /// BR-25. **This assertion is vacuous today and that is intentional -- do not delete it as
    /// dead weight.** There is no `img` element anywhere in the app or the landing page: every
    /// icon is either an emoji character, CSS generated content (`.bi-x::before`), or an inline
    /// SVG file referenced from the manifest rather than rendered into the page. So there is
    /// nothing to fail on, and the count assertion below records that fact rather than
    /// pretending to check something.
    ///
    /// It is a guard for the first image anyone adds: at that moment this starts asserting
    /// that it carries an `alt`, decorative or otherwise. `alt=""` is correct for a decorative
    /// image and passes; a MISSING alt attribute fails.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(FullViewportsByPage))]
    public async Task NoImage_LacksAltText(Viewport viewport, string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(viewport, path);

        var images = await session.Page.EvaluateAsync<string[]>(
            "() => Array.from(document.querySelectorAll('img')).map(el => el.getAttribute('src') || '(no src)')");
        var withoutAlt = await session.Page.EvaluateAsync<string[]>(
            "() => Array.from(document.querySelectorAll('img:not([alt])')).map(el => el.getAttribute('src') || '(no src)')");

        var where = $"{path} at {viewport}";

        withoutAlt.Should().BeEmpty(
            $"{where}: every img needs an alt attribute -- alt=\"\" for a decorative image, "
            + $"real text otherwise; missing on: {string.Join(", ", withoutAlt)}");

        // Pins the vacuity itself, so "0 images" is a recorded fact rather than a silent
        // assumption. When the first image is added this fails, which is the prompt to read the
        // comment above and drop this line -- the assertion above then does real work.
        images.Should().BeEmpty(
            $"{where}: the app ships no img elements today (icons are emoji, CSS content or "
            + "manifest SVGs), so the alt assertion above is a guard rather than a live check. "
            + $"If an image was deliberately added, delete this line: {string.Join(", ", images)}");
    }

    // ================================================================================
    // The rest of the structural rule set, delegated to axe
    // ================================================================================

    /// <summary>
    /// The structural rules axe evaluates better than a hand-written check would -- landmark
    /// uniqueness and nesting, content outside landmarks, empty headings, accessible names on
    /// buttons and links, image roles, and ARIA attribute validity. Pinned to the known set for
    /// the same reason as everything else here.
    ///
    /// `label` is in the rule set on purpose even though it passes: it is what makes the
    /// stricter hand-written BR-23 check above legible. axe passing while
    /// FormControls_HaveProgrammaticLabels fails is the documented difference between "has some
    /// accessible name" and "has a programmatic label", not a contradiction.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(FullViewportsByPage))]
    public async Task AxeStructuralRules_MatchTheKnownBaseline(Viewport viewport, string path)
    {
        SkipIfUnavailable();
        await using var session = await OpenAsync(viewport, path);

        var result = await session.Page.RunAxe(new AxeRunOptions
        {
            RunOnly = RunOnlyOptions.Rules(new[]
            {
                "landmark-one-main", "landmark-banner-is-top-level", "landmark-unique",
                "landmark-complementary-is-top-level", "region",
                "heading-order", "page-has-heading-one", "empty-heading",
                "label", "select-name", "form-field-multiple-labels",
                "button-name", "link-name", "input-button-name",
                "image-alt", "role-img-alt", "aria-valid-attr-value", "aria-allowed-attr",
            }),
            ResultTypes = [ResultType.Violations],
        });

        var violated = result.Violations.Select(v => v.Id).OrderBy(id => id).ToArray();
        var where = $"{path} at {viewport}";

        // BOTH pages are now clean on every structural rule. The tool page carried two until
        // W-15: heading-order (F-03) and select-name (the one third of F-04 axe could see --
        // the other two controls hid behind a placeholder, which is why BR-23 is also checked
        // by hand above). Anything appearing here is a regression.
        var detail = string.Join("; ", result.Violations.Select(v =>
            $"{v.Id} -> {string.Join(", ", v.Nodes.Select(n => n.Target.ToString()))}"));

        violated.Should().BeEmpty(
            $"{where}: no structural rule in this set should be violated on either page. "
            + $"Observed: {detail}");
    }
}
