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
/// Three of these are known to fail today, and the tests are written to CATCH them rather than
/// around them. As with F-12 and F-16, each known failure is PINNED to its exact offending
/// elements rather than asserted away or skipped: CR-01 means a permanently red assertion
/// blocks every merge on a protected branch, and AC-01 forbids skipping. A pin fails in both
/// directions -- a new violation is a regression, and a violation that disappears means the
/// baseline is stale and must be updated by whoever repaired it.
///
///   * F-01 -- no `contentinfo` landmark on either page. REPORTED, not repaired: the tool page
///     has no footer at all, and the landing page's `footer.ft-landing-footer` sits inside
///     `article.content` inside `main`, where a `footer` element does not expose the landmark
///     role. Adding one is a redesign under D-10, so it is Riley's, not W-15's.
///   * F-03 -- the tool page skips heading levels: `h1` "Manage My Money" straight to the `h5`
///     card headers. W-15 repairs this. The LANDING page does NOT skip (h1 then h2) -- it is
///     only the tool page, which is worth knowing before anyone "fixes" the landing page.
///   * F-04 -- three entry-form controls have no programmatic label. W-15 repairs this.
///
/// A NOTE ON WHY BR-23 IS HAND-WRITTEN AND NOT DELEGATED TO AXE. axe's `label` rule **passes**
/// on the tool page as it stands. That is not an axe bug: `placeholder` genuinely does
/// contribute to the accessible name as a last resort, so by axe's reckoning the money input
/// and the description input are named. BR-23 is deliberately stricter -- "a `dt` prompt
/// sitting next to an input is not a label", and neither is a placeholder, which vanishes as
/// soon as the user types. Delegating BR-23 to axe would have reported the tool page as
/// passing and F-04 would never have surfaced. Only the `select` has no placeholder to hide
/// behind, which is why axe catches that one alone.
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
    /// Nested inside main/article/section/aside/nav they are generic, which is the whole of
    /// F-01 on the landing page: the footer element exists and looks right in the markup, and
    /// is not a landmark. A naive `document.querySelector('footer')` would report it present.
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

        // F-01, PINNED. There is no contentinfo on either page and this item does not add one.
        // Asserting the presence it should have would leave the gate red for a defect nothing
        // downstream is authorised to repair -- adding a tool-page footer is a redesign (D-10),
        // so it is Riley's call. Pinned as ABSENT so that the moment a real footer landmark
        // appears this fails and says to delete the pin.
        counts.GetValueOrDefault("contentinfo").Should().Be(
            0,
            $"{where}: F-01 -- neither page exposes a contentinfo landmark today. The tool page "
            + "has no footer at all; the landing page's footer.ft-landing-footer is inside "
            + "article.content inside main, where a footer element is generic rather than a "
            + "landmark. This pins the defect so the gate stays green (CR-01). IF THIS FAILS, a "
            + "contentinfo landmark was added -- that is the fix: delete this assertion and "
            + "require contentinfo == 1 instead");
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

        if (path == SitePage.Landing)
        {
            // The landing page is CORRECT: h1 then h2. Asserted rather than assumed, because
            // F-03 is easy to read as "the site skips heading levels" when only one page does,
            // and a repair aimed at both would change a page that is already right.
            skips.Should().BeEmpty($"{where} should not skip heading levels; found: {string.Join("; ", skips)}");
            return;
        }

        // F-03, PINNED. The tool page jumps h1 -> h5: MoneyFlowCard renders its card header in
        // an <h5>, and so does the sticky net-total detail. W-15 repairs this by choosing
        // levels that descend properly. Exactly one skip is expected -- the first card header
        // after the h1; the four card headers after it are all h5 and so are level-flat, which
        // is legal.
        skips.Should().HaveCount(
            1,
            $"{where}: F-03 -- exactly one known heading-level skip is expected here, from the h1 "
            + $"to the first h5 card header. Found: {string.Join("; ", skips)}");
        skips[0].Should().StartWith(
            "h1 ",
            $"{where}: F-03 -- the known skip runs from the page h1; found {skips[0]}");
        skips[0].Should().Contain(
            "-> h5",
            $"{where}: F-03 -- the known skip lands on an h5 card header; found {skips[0]}. IF THIS "
            + "FAILS BECAUSE THERE IS NO SKIP, W-15 repaired it: delete this pin and assert "
            + "skips.Should().BeEmpty() for both pages");
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

        if (path == SitePage.Landing)
        {
            unlabelled.Should().BeEmpty(
                $"{where} has no form controls at all, so none can be unlabelled; found: "
                + string.Join("; ", unlabelled));
            return;
        }

        // F-04, PINNED -- the three entry-form controls in MoneyFlowCard.razor. Two hide behind
        // a placeholder (which is why axe's `label` rule passes them); the select has nothing
        // at all. W-15 repairs all three.
        var expected = new[]
        {
            "input.ft-input-money__field [placeholder=\"xxx.xx\"]",
            "select.ft-period-select [no placeholder]",
            "input [placeholder=\"Income Description\"]",
        };

        unlabelled.Should().BeEquivalentTo(
            expected,
            $"{where}: F-04 -- exactly these three entry-form controls lack a programmatic label, "
            + "and no others. A placeholder is not a label: it is not exposed as one to every "
            + "assistive technology and it disappears as soon as the user types. This pins the "
            + "known set so the gate stays green (CR-01) while any NEW unlabelled control fails. "
            + "IF THIS FAILS BECAUSE THE LIST IS NOW EMPTY, W-15 repaired it: delete this pin and "
            + "assert unlabelled.Should().BeEmpty() for both pages");
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

        // Landing is clean on every structural rule. Tool has exactly two, both already owned:
        // heading-order is F-03 and select-name is the one third of F-04 that axe can see.
        var expected = path == SitePage.Landing
            ? Array.Empty<string>()
            : ["heading-order", "select-name"];

        var detail = string.Join("; ", result.Violations.Select(v =>
            $"{v.Id} -> {string.Join(", ", v.Nodes.Select(n => n.Target.ToString()))}"));

        violated.Should().BeEquivalentTo(
            expected,
            $"{where}: the structural rule violations should be exactly the known set. On the "
            + "tool page that is heading-order (F-03) and select-name (the one part of F-04 axe "
            + "can see -- the other two controls hide behind a placeholder). A rule appearing "
            + "here is a regression; a rule disappearing means W-15 repaired it and this "
            + $"baseline needs updating. Observed: {detail}");
    }
}
