using FluentAssertions;
using Xunit;

namespace Footing.Tests.E2E;

[Collection("Playwright")]
public class LayoutAssertionsTests
{
    private const string ContentSelector = "article.content";
    private const double MainCapPx = 864;
    private const double GutterTolerancePx = 2;
    private const double MinGutterPx = 16;

    private readonly PlaywrightFixture _fixture;
    public LayoutAssertionsTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    // R-03 boundary: the gutter is exactly 16px at Mobile/MobileFloor/Tablet by design
    // (main's `padding: 0 1rem` with `.content`'s zero horizontal padding), so this
    // assertion sits exactly on the boundary on the unmodified stylesheet.
    public static IEnumerable<object[]> BoundaryViewportsByPage =>
        from viewport in new[] { Viewports.MobileFloor, Viewports.Mobile, Viewports.Tablet }
        from page in new[] { "/", "/find-my-footing/" }
        select new object[] { viewport, page };

    [SkippableTheory]
    [MemberData(nameof(Viewports.AllByPage), MemberType = typeof(Viewports))]
    public async Task NoHorizontalOverflow_OnCurrentSite(Viewport viewport, string page)
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(viewport);
        await session.Page.GotoAsync($"{_fixture.BaseUrl}{page}");
        await LayoutAssertions.AssertNoHorizontalOverflowAsync(
            session.Page, $"{viewport} {page} should not overflow horizontally");
    }

    [SkippableFact]
    public async Task MainCappedAndCentered_AtDesktop()
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Desktop);
        await session.Page.GotoAsync($"{_fixture.BaseUrl}/find-my-footing/");
        await session.Page.Locator("main").WaitForAsync();
        await LayoutAssertions.AssertMainCappedAndCenteredAsync(session.Page, MainCapPx, GutterTolerancePx);
    }

    [SkippableTheory]
    [MemberData(nameof(BoundaryViewportsByPage))]
    public async Task ContentGutter_AtBoundary(Viewport viewport, string page)
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(viewport);
        await session.Page.GotoAsync($"{_fixture.BaseUrl}{page}");
        await session.Page.Locator(ContentSelector).WaitForAsync();
        await LayoutAssertions.AssertContentGutterAsync(session.Page, ContentSelector, MinGutterPx, GutterTolerancePx);
    }

    [SkippableFact]
    public async Task NoHorizontalOverflow_FailureMessage_NamesOffendingElement()
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Mobile);
        await session.Page.GotoAsync($"{_fixture.BaseUrl}/find-my-footing/");
        await session.Page.EvaluateAsync(
            """
            () => {
                const el = document.createElement('div');
                el.id = 'layout-assertions-overflow-probe';
                el.style.cssText = 'position:absolute;top:0;left:0;width:5000px;height:10px;';
                document.body.appendChild(el);
            }
            """);

        var act = async () => await LayoutAssertions.AssertNoHorizontalOverflowAsync(session.Page, "forced overflow probe");

        var thrown = await act.Should().ThrowAsync<Exception>();
        thrown.Which.Message.Should().Contain("layout-assertions-overflow-probe");
    }
}
