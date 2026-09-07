using FluentAssertions;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// Tests of the assertion library's own behaviour.
///
/// The site-level theories that stood here after W-02 -- overflow across the matrix, the
/// gutter at the mobile/tablet boundary, and cap-and-centre at desktop -- moved into
/// HomePageTests.HomePage_LayoutContractHolds and
/// FindMyFootingPageTests (LayoutContractHolds, plus the two narrow-viewport tests that
/// carry the tool page's quarantined overflow defect) in W-04, which between them run all
/// three assertions over all four viewports on both pages. That is a strict superset of what
/// was here: the gutter gained the 320 floor, cap-and-centre gained the landing page, and
/// both gained a wait for the WASM app to render -- without which every tool-page measurement
/// was being taken against the "Loading&hellip;" placeholder, and the overflow defect that
/// W-04 surfaced at 320 and 375 went unseen. Keeping both sets would have doubled the browser
/// sessions on the protected-branch gate for no additional coverage (CR-01).
///
/// What stays is the part no site-level test covers: that a failure names the element that
/// caused it.
/// </summary>
[Collection("Playwright")]
public class LayoutAssertionsTests
{
    private readonly PlaywrightFixture _fixture;
    public LayoutAssertionsTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    [SkippableFact]
    public async Task NoHorizontalOverflow_FailureMessage_NamesOffendingElement()
    {
        SkipIfUnavailable();
        await using var session = await _fixture.NewSessionAsync(Viewports.Mobile);
        await session.Page.GotoAsync($"{_fixture.BaseUrl}{SitePage.Tool}");
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
