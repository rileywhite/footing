using Footing.Models;
using Nito.AsyncEx;
using Simplexcel;

namespace Footing.Client.Library;

public class SimplexcelExporter
{
    private static readonly string DollarFormat = "$#,##0.00;($#,##0.00)";
    private static readonly int NameColumnWidth = 20;
    private static readonly int AmountColumnWidth = 10;

    public SimplexcelExporter(FootingAnalysis footingAnalysis)
    {
        FootingAnalysis = footingAnalysis;
        Workbook = new AsyncLazy<Workbook>(InitializeWorkbook);
    }

    private FootingAnalysis FootingAnalysis { get; }

    private AsyncLazy<Workbook> Workbook { get; }

    private async Task<Workbook> InitializeWorkbook()
    {
        await Task.Yield();

        (Worksheet Worksheet, MoneyFlowDirection Direction)[] detailWorksheets =
        [
            (GenerateIncomeWorksheet("Income", FootingAnalysis.Inflows), FootingAnalysis.Inflows.Direction),
            (GenerateBudgetWorksheet("Recurring Bills", FootingAnalysis.RecurringBills), FootingAnalysis.RecurringBills.Direction),
            (GenerateBudgetWorksheet("Household Budget", FootingAnalysis.HouseholdBudgets), FootingAnalysis.HouseholdBudgets.Direction),
            (GenerateBudgetWorksheet("Personal Budget", FootingAnalysis.PersonalBudgets), FootingAnalysis.PersonalBudgets.Direction),
            (GenerateBudgetWorksheet("Events Budget", FootingAnalysis.EventBudgets), FootingAnalysis.EventBudgets.Direction),
        ];

        var summary = GenerateSummaryWorksheet(detailWorksheets);

        var workbook = new Workbook();
        workbook.Add(summary);
        foreach (var detailWorksheet in detailWorksheets)
        {
            workbook.Add(detailWorksheet.Worksheet);
        }

        return workbook;
    }

    private Worksheet GenerateSummaryWorksheet((Worksheet Worksheet, MoneyFlowDirection Direction)[] detailWorksheets)
    {
        var worksheet = new Worksheet("Summary");

        worksheet.ColumnWidths[0] = NameColumnWidth;
        worksheet.ColumnWidths[1] = AmountColumnWidth;

        worksheet["A1"] = "Manage Your Finances at https://footing.app";
        worksheet["A1"].Hyperlink = "https://footing.app";
        worksheet["A1"].Bold = true;

        worksheet["B3"] = "Weekly Avg";
        worksheet["B3"].Bold = true;
        worksheet["B3"].HorizontalAlignment = HorizontalAlign.Center;

        for (var i = 0; i < detailWorksheets.Length; i++)
        {
            var detailWorksheet = detailWorksheets[i].Worksheet;
            var direction = detailWorksheets[i].Direction;
            int rowIndex = i + 3;

            worksheet[rowIndex, 0] = detailWorksheet.Name;
            worksheet[rowIndex, 0].Bold = true;

            worksheet[rowIndex, 1] =
                direction == MoneyFlowDirection.Income ?
                    Cell.Formula($"'{detailWorksheet.Name}'!$D$1") :
                    Cell.Formula($"-'{detailWorksheet.Name}'!$D$1");

            worksheet[rowIndex, 1].Format = DollarFormat;
        }

        var totalRowIndex = detailWorksheets.Length + 6;
        var lastDetailRowNum = detailWorksheets.Length + 3;

        worksheet[totalRowIndex, 0] = "Net";
        worksheet[totalRowIndex, 0].Bold = true;

        worksheet[totalRowIndex, 1] = Cell.Formula($"SUM(B$2:B${lastDetailRowNum})");
        worksheet[totalRowIndex, 1].Format = DollarFormat;

        return worksheet;
    }

    private Worksheet GenerateIncomeWorksheet(string name, MoneyFlows moneyFlows)
        => GenerateWorksheet(name, moneyFlows, "Source", "Net");

    private Worksheet GenerateBudgetWorksheet(string name, MoneyFlows moneyFlows)
        => GenerateWorksheet(name, moneyFlows, "Expense", "Amount");

    private Worksheet GenerateWorksheet(
        string name,
        MoneyFlows moneyFlows,
        string nameColumnHeader,
        string amountColumnHeader)
    {
        var worksheet = new Worksheet(name);

        worksheet.ColumnWidths[0] = NameColumnWidth;
        worksheet.ColumnWidths[1] = AmountColumnWidth;
        worksheet.ColumnWidths[2] = AmountColumnWidth;
        worksheet.ColumnWidths[3] = AmountColumnWidth;

        worksheet["C1"] = "Total";
        worksheet["C1"].Bold = true;

        worksheet["D1"] = Cell.Formula("SUM(D3:D1000)");

        worksheet["D1"].Format = DollarFormat;
        worksheet["E1"].Format = DollarFormat;
        worksheet["F1"].Format = DollarFormat;

        worksheet["A2"] = nameColumnHeader;
        worksheet["B2"] = amountColumnHeader;
        worksheet["C2"] = "Periods/Yr";
        worksheet["D2"] = "Avg Weekly";

        worksheet["A2"].Bold = true;
        worksheet["B2"].Bold = true;
        worksheet["C2"].Bold = true;
        worksheet["D2"].Bold = true;

        worksheet["A2"].HorizontalAlignment = HorizontalAlign.Center;
        worksheet["B2"].HorizontalAlignment = HorizontalAlign.Center;
        worksheet["C2"].HorizontalAlignment = HorizontalAlign.Center;
        worksheet["D2"].HorizontalAlignment = HorizontalAlign.Center;

        for (var i = 0; i < moneyFlows.Count; i++)
        {
            var moneyFlow = moneyFlows[i];
            var rowIndex = i + 2;
            var rowNum = i + 3;

            worksheet[rowIndex, 0] = moneyFlow.Name;
            worksheet[rowIndex, 0].Bold = true;

            worksheet[rowIndex, 1] = Math.Abs(moneyFlow.Amount);
            worksheet[rowIndex, 1].Format = DollarFormat;

            worksheet[rowIndex, 2] = moneyFlow.Period.PeriodsPerYear();

            worksheet[rowIndex, 3] = Cell.Formula($"$B{rowNum} * $C{rowNum} / 52");
            worksheet[rowIndex, 3].Format = DollarFormat;
        }

        return worksheet;
    }

    public async Task ExportTo(Stream stream) => (await Workbook).Save(stream);
}
