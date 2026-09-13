using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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

    private static readonly XNamespace SpreadsheetMl = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private sealed record DetailRow(decimal Amount, decimal PeriodsPerYear, string AvgWeeklyFormula);

    // Reads the data rows of a detail worksheet straight out of the exported .xlsx package.
    private static async Task<IReadOnlyList<DetailRow>> ReadDetailRows(FootingAnalysis analysis, string sheetPath)
    {
        using var stream = new MemoryStream();
        await new SimplexcelExporter(analysis).ExportTo(stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var sheet = archive.GetEntry(sheetPath)!.Open();
        var cells = XDocument.Load(sheet).Descendants(SpreadsheetMl + "c")
            .ToDictionary(c => (string)c.Attribute("r")!);

        var rows = new List<DetailRow>();
        for (var rowNum = 3; cells.ContainsKey($"B{rowNum}"); rowNum++)
        {
            rows.Add(new DetailRow(
                decimal.Parse(cells[$"B{rowNum}"].Element(SpreadsheetMl + "v")!.Value, CultureInfo.InvariantCulture),
                decimal.Parse(cells[$"C{rowNum}"].Element(SpreadsheetMl + "v")!.Value, CultureInfo.InvariantCulture),
                cells[$"D{rowNum}"].Element(SpreadsheetMl + "f")!.Value));
        }

        return rows;
    }

    // Evaluates the Avg Weekly formula the way a spreadsheet does: IEEE doubles, not decimal.
    private static double EvaluateAvgWeekly(DetailRow row, int rowNum)
    {
        var match = Regex.Match(row.AvgWeeklyFormula, @"^\$B(\d+) \* \$C(\d+) / (\d+(?:\.\d+)?)$");
        match.Success.Should().BeTrue("the Avg Weekly formula '{0}' should have the shape $Bn * $Cn / weeksPerYear", row.AvgWeeklyFormula);
        match.Groups[1].Value.Should().Be(rowNum.ToString(CultureInfo.InvariantCulture));
        match.Groups[2].Value.Should().Be(rowNum.ToString(CultureInfo.InvariantCulture));
        var divisor = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        return (double)row.Amount * (double)row.PeriodsPerYear / divisor;
    }

    private static FootingAnalysis CreateOneIncomePerPeriodAnalysis(decimal amount)
    {
        var analysis = new FootingAnalysis();
        foreach (var period in Enum.GetValues<Period>())
            analysis.Inflows.Add(new MoneyFlow { Name = period.ToString(), Amount = amount, Period = period });
        return analysis;
    }

    [Fact]
    public async Task ExportTo_AvgWeekly_AgreesWithAppForEveryPeriod()
    {
        var analysis = CreateOneIncomePerPeriodAnalysis(1234.56m);
        var rows = await ReadDetailRows(analysis, "xl/worksheets/sheet2.xml");

        rows.Should().HaveCount(Enum.GetValues<Period>().Length);
        for (var i = 0; i < rows.Count; i++)
        {
            var moneyFlow = analysis.Inflows[i];
            var appWeekly = moneyFlow.GetWeeklyAmount();

            rows[i].Amount.Should().Be(moneyFlow.Amount);
            rows[i].PeriodsPerYear.Should().Be(moneyFlow.Period.PeriodsPerYear());

            var spreadsheetWeekly = EvaluateAvgWeekly(rows[i], i + 3);
            ((decimal)spreadsheetWeekly).Should().BeApproximately(appWeekly, 0.000001m, "{0}", moneyFlow.Period);
            Math.Round((decimal)spreadsheetWeekly, 2).Should().Be(((MonetaryAmount)appWeekly).RoundedAmount.Amount, "{0}", moneyFlow.Period);
        }
    }

    [Fact]
    public async Task ExportTo_AvgWeeklyFormula_DividesByPreciseWeeksPerYear()
    {
        var rows = await ReadDetailRows(CreateOneIncomePerPeriodAnalysis(100m), "xl/worksheets/sheet2.xml");

        rows.Should().NotBeEmpty();
        rows.Should().AllSatisfy(row => row.AvgWeeklyFormula.Should().EndWith(" / 52.1775"));
    }

    [Fact]
    public async Task ExportTo_UnderCommaDecimalCulture_WritesInvariantNumbers()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var rows = await ReadDetailRows(CreateOneIncomePerPeriodAnalysis(100m), "xl/worksheets/sheet2.xml");

            rows.Should().AllSatisfy(row => row.AvgWeeklyFormula.Should().EndWith(" / 52.1775"));
            rows.Select(row => row.PeriodsPerYear).Should().Equal(
                Enum.GetValues<Period>().Select(period => period.PeriodsPerYear()));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
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
