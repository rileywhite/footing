using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Footing.Tests.E2E;

/// <summary>
/// BR-16: the Excel export produces a real file, not merely a present button.
/// </summary>
[Collection("Playwright")]
public class ExcelExportTests
{
    private const string ExportButtonSelector = "input.ft-export-btn";

    private readonly PlaywrightFixture _fixture;
    public ExcelExportTests(PlaywrightFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable() =>
        Skip.If(!_fixture.ServerAvailable, "Server not available");

    // Desktop and Mobile only, per the item: the export button sits in .ft-compact-actions
    // and its reachability differs between the wide and narrow arrangements, which is what
    // makes a second viewport worth its browser session. Tablet and the 320 floor would add
    // runtime to the protected gate (CR-01) without adding a distinct layout.
    [SkippableTheory]
    [MemberData(nameof(Viewports.DesktopAndMobile), MemberType = typeof(Viewports))]
    public async Task ExportButton_DownloadsAnXlsxFile(Viewport viewport)
    {
        SkipIfUnavailable();

        // D-05: downloads are enabled EXPLICITLY on the context. OQ-03 asked whether the
        // default accepts downloads; the answer is not to depend on it either way, so this
        // says so rather than inheriting whatever Playwright currently does.
        await using var session = await _fixture.NewSessionAsync(
            viewport,
            acceptDownloads: true,
            localStorageSeed: ToolStorage.ReturningUser());

        await session.Page.GotoAsync($"{_fixture.BaseUrl}{SitePage.Tool}");
        await session.Page.WaitForSelectorAsync(
            "#moneyFlows", new() { Timeout = 60000, State = WaitForSelectorState.Attached });

        var exportButton = session.Page.Locator(ExportButtonSelector);
        await exportButton.WaitForAsync(new() { Timeout = 15000 });

        var download = await session.Page.RunAndWaitForDownloadAsync(
            async () => await exportButton.ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 30000 });

        download.SuggestedFilename.Should().Be(
            "Footing.xlsx", "the export names the file in FootingAnalysisEditor.ExportToExcel");

        var path = Path.Combine(Path.GetTempPath(), $"footing-export-{Guid.NewGuid():N}.xlsx");
        try
        {
            await download.SaveAsAsync(path);

            // Deliberately not parsed. Workbook CONTENT is
            // Footing.Tests.Integration/SimplexcelExporterTests' job (D-05, OQ-03);
            // duplicating it here buys nothing and costs runtime on the gate. What this test
            // owns is the seam that project cannot reach: that clicking the button in a real
            // browser actually produces a non-empty file with the right name.
            new FileInfo(path).Length.Should().BeGreaterThan(
                0, "an exported workbook with a seeded income entry must not be empty");
        }
        finally
        {
            // finally, not a trailing delete: an assertion failure above must not leave the
            // temp file behind.
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
