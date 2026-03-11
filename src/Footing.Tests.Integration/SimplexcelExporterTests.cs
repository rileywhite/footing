using System.IO.Compression;
using FluentAssertions;
using Footing.Client.Library;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Integration;

public class SimplexcelExporterTests
{
    private static FootingAnalysis CreateTestAnalysis()
    {
        var analysis = new FootingAnalysis();
        analysis.Inflows.Add(new MoneyFlow { Name = "Salary", Amount = 2000m, Period = Period.BiWeekly });
        analysis.Inflows.Add(new MoneyFlow { Name = "Freelance", Amount = 500m, Period = Period.Monthly });
        analysis.RecurringBills.Add(new MoneyFlow { Name = "Rent", Amount = 1500m, Period = Period.Monthly });
        analysis.RecurringBills.Add(new MoneyFlow { Name = "Phone", Amount = 80m, Period = Period.Monthly });
        analysis.HouseholdBudgets.Add(new MoneyFlow { Name = "Groceries", Amount = 150m, Period = Period.Weekly });
        analysis.PersonalBudgets.Add(new MoneyFlow { Name = "Lunch", Amount = 10m, Period = Period.Daily });
        analysis.EventBudgets.Add(new MoneyFlow { Name = "Christmas", Amount = 500m, Period = Period.Annually });
        return analysis;
    }

    [Fact]
    public async Task ExportTo_ProducesNonEmptyStream()
    {
        using var stream = new MemoryStream();
        await new SimplexcelExporter(CreateTestAnalysis()).ExportTo(stream);
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportTo_ProducesValidZipArchive()
    {
        using var stream = new MemoryStream();
        await new SimplexcelExporter(CreateTestAnalysis()).ExportTo(stream);
        stream.Position = 0;
        var header = new byte[4];
        stream.Read(header, 0, 4);
        header[0].Should().Be(0x50); // P
        header[1].Should().Be(0x4B); // K
    }

    [Fact]
    public async Task ExportTo_ContainsWorkbookXml()
    {
        using var stream = new MemoryStream();
        await new SimplexcelExporter(CreateTestAnalysis()).ExportTo(stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.Entries.Select(e => e.FullName).Should().Contain(e => e.Contains("workbook.xml"));
    }

    [Fact]
    public async Task ExportTo_ContainsSixSheets()
    {
        using var stream = new MemoryStream();
        await new SimplexcelExporter(CreateTestAnalysis()).ExportTo(stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.Entries.Where(e => e.FullName.Contains("sheet") && e.FullName.EndsWith(".xml"))
            .Should().HaveCount(6);
    }

    [Fact]
    public async Task ExportTo_EmptyAnalysis_ProducesValidExcel()
    {
        using var stream = new MemoryStream();
        await new SimplexcelExporter(new FootingAnalysis()).ExportTo(stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.Entries.Where(e => e.FullName.Contains("sheet") && e.FullName.EndsWith(".xml"))
            .Should().HaveCount(6);
    }

    [Fact]
    public async Task ExportTo_WorkbookContainsSheetNames()
    {
        using var stream = new MemoryStream();
        await new SimplexcelExporter(CreateTestAnalysis()).ExportTo(stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.Entries.First(e => e.FullName.Contains("workbook.xml") && !e.FullName.Contains("rels"));
        using var reader = new StreamReader(entry.Open());
        var xml = reader.ReadToEnd();
        xml.Should().Contain("Summary");
        xml.Should().Contain("Income");
        xml.Should().Contain("Recurring Bills");
        xml.Should().Contain("Household Budget");
        xml.Should().Contain("Personal Budget");
        xml.Should().Contain("Events Budget");
    }

    [Fact]
    public async Task ExportTo_CanBeCalledMultipleTimes()
    {
        var exporter = new SimplexcelExporter(CreateTestAnalysis());
        using var stream1 = new MemoryStream();
        await exporter.ExportTo(stream1);
        using var stream2 = new MemoryStream();
        await exporter.ExportTo(stream2);
        stream1.Length.Should().BeGreaterThan(0);
        stream2.Length.Should().BeGreaterThan(0);
        stream1.Position = 0;
        stream2.Position = 0;
        using var a1 = new ZipArchive(stream1, ZipArchiveMode.Read);
        using var a2 = new ZipArchive(stream2, ZipArchiveMode.Read);
        a1.Entries.Count.Should().Be(a2.Entries.Count);
    }
}
