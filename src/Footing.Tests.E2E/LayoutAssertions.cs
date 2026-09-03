using FluentAssertions;
using Microsoft.Playwright;

namespace Footing.Tests.E2E;

/// <summary>
/// Reusable layout assertions shared across viewport/responsiveness tests.
/// </summary>
public static class LayoutAssertions
{
    private sealed class OverflowMetrics
    {
        public int ScrollWidth { get; set; }
        public int ClientWidth { get; set; }
    }

    private sealed class OffendingElement
    {
        public string Tag { get; set; } = "";
        public string Id { get; set; } = "";
        public string ClassList { get; set; } = "";
        public double Right { get; set; }
    }

    private sealed class EdgeRect
    {
        public double Left { get; set; }
        public double Right { get; set; }
        public double Width { get; set; }
    }

    /// <summary>
    /// Asserts the document does not overflow horizontally. On failure, names every
    /// element whose right edge crosses the client width so the failure is actionable
    /// without a debugger.
    /// </summary>
    public static async Task AssertNoHorizontalOverflowAsync(IPage page, string because)
    {
        var metrics = await page.EvaluateAsync<OverflowMetrics>(
            "() => ({ scrollWidth: document.documentElement.scrollWidth, clientWidth: document.documentElement.clientWidth })");

        if (metrics.ScrollWidth <= metrics.ClientWidth)
        {
            return;
        }

        var offenders = await page.EvaluateAsync<OffendingElement[]>(
            """
            () => Array.from(document.querySelectorAll('*'))
                .map(el => ({ el, rect: el.getBoundingClientRect() }))
                .filter(x => x.rect.right > document.documentElement.clientWidth + 1)
                .map(x => ({
                    tag: x.el.tagName.toLowerCase(),
                    id: x.el.id || '',
                    classList: Array.from(x.el.classList).join(' '),
                    right: x.rect.right
                }))
            """);

        var description = offenders.Length == 0
            ? "no offending element could be identified"
            : string.Join("; ", offenders.Select(o =>
                $"<{o.Tag}{(o.Id.Length > 0 ? $" id=\"{o.Id}\"" : "")}{(o.ClassList.Length > 0 ? $" class=\"{o.ClassList}\"" : "")}> right={o.Right}"));

        metrics.ScrollWidth.Should().BeLessThanOrEqualTo(
            metrics.ClientWidth,
            $"{because}; overflowing elements: {description}");
    }

    /// <summary>
    /// Asserts `main` is capped at <paramref name="capPx"/> and centred, per D-02(b).
    /// Only meaningful once the viewport exceeds the cap -- below it `main` legitimately
    /// fills the viewport, so callers must only invoke this where
    /// <c>documentElement.clientWidth &gt; capPx</c>.
    /// </summary>
    public static async Task AssertMainCappedAndCenteredAsync(IPage page, double capPx, double tolerancePx)
    {
        var clientWidth = await page.EvaluateAsync<double>("() => document.documentElement.clientWidth");
        var rect = await page.EvalOnSelectorAsync<EdgeRect>(
            "main",
            "el => { const r = el.getBoundingClientRect(); return { left: r.left, right: r.right, width: r.width }; }");

        rect.Width.Should().BeApproximately(
            capPx,
            tolerancePx,
            $"main should be capped at {capPx}px (measured from documentElement.clientWidth={clientWidth})");

        var leftGutter = rect.Left;
        var rightGutter = clientWidth - rect.Right;
        Math.Abs(leftGutter - rightGutter).Should().BeLessThanOrEqualTo(
            tolerancePx,
            $"main should be centred (left gutter={leftGutter}, right gutter={rightGutter})");
    }

    /// <summary>
    /// Asserts the content gutter on <paramref name="contentSelector"/> is at least
    /// <paramref name="minGutterPx"/>, tolerant to <paramref name="tolerancePx"/> and
    /// measured from documentElement.clientWidth (not the nominal viewport width) so a
    /// scrollbar cannot make a correctly gutter'd page look wrong.
    ///
    /// At Mobile, MobileFloor and Tablet the gutter is exactly 16px by design (main's
    /// `padding: 0 1rem` with `.content`'s zero horizontal padding) -- this assertion sits
    /// exactly on that boundary on purpose. Do not tighten it.
    /// </summary>
    public static async Task AssertContentGutterAsync(IPage page, string contentSelector, double minGutterPx, double tolerancePx)
    {
        var clientWidth = await page.EvaluateAsync<double>("() => document.documentElement.clientWidth");
        var rect = await page.EvalOnSelectorAsync<EdgeRect>(
            contentSelector,
            "el => { const r = el.getBoundingClientRect(); return { left: r.left, right: r.right, width: r.width }; }");

        var leftGutter = rect.Left;
        var rightGutter = clientWidth - rect.Right;
        var gutter = Math.Min(leftGutter, rightGutter);

        gutter.Should().BeGreaterThanOrEqualTo(
            minGutterPx - tolerancePx,
            $"'{contentSelector}' should keep at least a {minGutterPx}px gutter (left={leftGutter}, right={rightGutter}, clientWidth={clientWidth})");
    }

    /// <summary>
    /// Reads computed style property values for the first element matching
    /// <paramref name="selector"/>.
    /// </summary>
    public static Task<Dictionary<string, string>> ReadComputedStylesAsync(
        IPage page, string selector, IReadOnlyList<string> properties)
    {
        return page.EvalOnSelectorAsync<Dictionary<string, string>>(
            selector,
            """
            (el, props) => {
                const cs = getComputedStyle(el);
                const result = {};
                for (const p of props) result[p] = cs.getPropertyValue(p);
                return result;
            }
            """,
            properties);
    }
}
