using Nito.AsyncEx;
using Simplexcel;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Remeter.Portal.Components
{
    public class SimplexcelExporter
    {
        private static string DollarFormat = "$#,##0.00";

        public SimplexcelExporter(RemeterAnalysis remeterAnalysis)
        {
            this.RemeterAnalysis = remeterAnalysis;
            this.Workbook = new AsyncLazy<Workbook>(this.InitializeWorkbook);
        }

        private RemeterAnalysis RemeterAnalysis { get; }

        private AsyncLazy<Workbook> Workbook { get; }

        private async Task<Workbook> InitializeWorkbook()
        {
            await Task.Yield();

            var income = this.GenerateIncomeWorksheet("Income", this.RemeterAnalysis.Inflows);
            var recurringBills = this.GenerateBudgetWorksheet("Recurring Bills", this.RemeterAnalysis.RecurringBills);
            var householdBudget = this.GenerateBudgetWorksheet("Household Budget", this.RemeterAnalysis.HouseholdBudgets);
            var personalBudget = this.GenerateBudgetWorksheet("Personal Budget", this.RemeterAnalysis.PersonalBudgets);
            var eventBudget = this.GenerateBudgetWorksheet("Events Budget", this.RemeterAnalysis.EventBudgets);

            var summary = new Worksheet("Summary");

            var cell = new Cell(CellType.Text);
            cell.Value = "hullo wurld";

            var worksheet = new Worksheet("Test");
            worksheet[0, 0] = cell;

            var workbook = new Workbook();
            workbook.Add(summary);
            workbook.Add(income);
            workbook.Add(recurringBills);
            workbook.Add(householdBudget);
            workbook.Add(personalBudget);
            workbook.Add(eventBudget);

            return workbook;
        }

        private Worksheet GenerateIncomeWorksheet(string name, MoneyFlows moneyFlows)
            => this.GenerateWorksheet(name, moneyFlows, "Source", "Net");

        private Worksheet GenerateBudgetWorksheet(string name, MoneyFlows moneyFlows)
            => this.GenerateWorksheet(name, moneyFlows, "Expense", "Amount");

        private Worksheet GenerateWorksheet(
            string name,
            MoneyFlows moneyFlows,
            string nameColumnHeader,
            string amountColumnHeader)
        {
            var worksheet = new Worksheet(name);

            worksheet["C1"] = "Totals";
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

        public async Task ExportTo(Stream stream) => (await this.Workbook).Save(stream);
    }
}
