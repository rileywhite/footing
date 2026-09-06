using FluentAssertions;
using Microsoft.Playwright;

namespace Footing.Tests.E2E;

/// <summary>
/// What both pages of the site have in common: their paths, how to open one so its real
/// content exists, and the layout contract every viewport has to satisfy (D-02).
/// </summary>
internal static class SitePage
{
    /// <summary>The hand-written static landing page.</summary>
    public const string Landing = "/";

    /// <summary>The Blazor WebAssembly tool, served under its own base path.</summary>
    public const string Tool = "/find-my-footing/";

    /// <summary>`main { max-width: 54rem }` in app.css, at the 16px root font size.</summary>
    public const double MainCapPx = 864;

    /// <summary>`main { padding: 0 1rem }`, with `.content` adding no horizontal padding.</summary>
    public const double MinGutterPx = 16;

    /// <summary>
    /// R-03: 2px, measured from documentElement.clientWidth rather than the nominal viewport
    /// width, so sub-pixel layout rounding and a scrollbar cannot make a correct page look wrong.
    /// </summary>
    public const double TolerancePx = 2;

    /// <summary>The text block inside `main`, present on both pages.</summary>
    public const string ContentSelector = "article.content";

    /// <summary>
    /// Navigates and waits for the page's real content to exist.
    ///
    /// Load-bearing for the tool page: its index.html ships a "Loading&hellip;" placeholder,
    /// and `main` / `article.content` only appear once the WASM runtime has booted and
    /// MainLayout has rendered. A layout or third-party-request assertion made against the
    /// placeholder passes vacuously -- there is nothing wide enough to overflow, and no
    /// _framework fetch has happened yet. On the landing page the element is in the served
    /// HTML and this returns immediately.
    /// </summary>
    public static async Task GotoRenderedAsync(
        IPage page, string baseUrl, string path, PageGotoOptions? options = null)
    {
        await page.GotoAsync($"{baseUrl}{path}", options);
        await page.Locator(ContentSelector).WaitForAsync(new() { Timeout = 60000 });
    }

    /// <summary>
    /// The three D-02 assertions, in the shape D-02 actually specifies: (a) no horizontal
    /// overflow at every viewport including the 320 floor; (c) the content gutter at every
    /// viewport; (b) cap-and-centre only where `documentElement.clientWidth` exceeds the cap,
    /// because below it `main` legitimately fills the viewport and the assertion would be
    /// false against a correct page.
    /// </summary>
    public static async Task AssertLayoutContractAsync(IPage page, Viewport viewport, string path)
    {
        var where = $"{path} at {viewport}";

        // D-02(a) -- every viewport.
        await LayoutAssertions.AssertNoHorizontalOverflowAsync(page, $"{where} should not overflow horizontally");

        // D-02(c) -- every viewport. This is the assertion that catches an edge-to-edge
        // page at mobile and tablet, where (b) below is vacuous by design.
        await LayoutAssertions.AssertContentGutterAsync(page, ContentSelector, MinGutterPx, TolerancePx);

        // D-02(b) -- only where the cap binds.
        var clientWidth = await page.EvaluateAsync<double>("() => document.documentElement.clientWidth");
        if (viewport.Width > MainCapPx)
        {
            // Ties the runtime condition back to the static viewport table so this branch
            // cannot go silently dark: a scrollbar costs ~15px at most, and Desktop clears
            // the cap by 416px, so a nominal width over the cap must measure over it too.
            clientWidth.Should().BeGreaterThan(
                MainCapPx,
                $"{where}: the {viewport.Width}px viewport is wider than the {MainCapPx}px cap, so "
                + "the cap-and-centre assertion below must actually run");
        }

        if (clientWidth > MainCapPx)
        {
            await LayoutAssertions.AssertMainCappedAndCenteredAsync(page, MainCapPx, TolerancePx);
        }
    }
}
