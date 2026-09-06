using FluentAssertions;
using Xunit;

namespace Footing.Tests.E2E;

[Collection("Playwright")]
public class ViewportSmokeTests
{
    private readonly PlaywrightFixture _fixture;
    public ViewportSmokeTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    [SkippableTheory]
    [MemberData(nameof(Viewports.AllByPage), MemberType = typeof(Viewports))]
    public async Task Page_LoadsThroughSession(Viewport viewport, string page)
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(viewport);
        var response = await session.Page.GotoAsync($"{_fixture.BaseUrl}{page}");
        response!.Status.Should().Be(200);
    }
}
