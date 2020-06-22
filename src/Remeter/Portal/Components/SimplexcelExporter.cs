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

            (Worksheet Worksheet, MoneyFlowDirection Direction)[] detailWorksheets = new[]
            {
                (this.GenerateIncomeWorksheet("Income", this.RemeterAnalysis.Inflows), this.RemeterAnalysis.Inflows.Direction),
                (this.GenerateBudgetWorksheet("Recurring Bills", this.RemeterAnalysis.RecurringBills), this.RemeterAnalysis.RecurringBills.Direction),
                (this.GenerateBudgetWorksheet("Household Budget", this.RemeterAnalysis.HouseholdBudgets), this.RemeterAnalysis.HouseholdBudgets.Direction),
                (this.GenerateBudgetWorksheet("Personal Budget", this.RemeterAnalysis.PersonalBudgets), this.RemeterAnalysis.PersonalBudgets.Direction),
                (this.GenerateBudgetWorksheet("Events Budget", this.RemeterAnalysis.EventBudgets), this.RemeterAnalysis.EventBudgets.Direction),
            };

            var summary = this.GenerateSummaryWorksheet(detailWorksheets);

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

            worksheet["B1"] = "Weekly Avg";
            worksheet["B1"].Bold = true;

            for (var i = 0; i < detailWorksheets.Length; i++)
            {
                var detailWorksheet = detailWorksheets[i].Worksheet;
                var direction = detailWorksheets[i].Direction;
                int rowIndex = i + 1;

                worksheet[rowIndex, 0] = detailWorksheet.Name;
                worksheet[rowIndex, 0].Bold = true;

                worksheet[rowIndex, 1] =
                    direction == MoneyFlowDirection.Income ?
                        Cell.Formula($"'{detailWorksheet.Name}'!$D$1") :
                        Cell.Formula($"-'{detailWorksheet.Name}'!$D$1");

                worksheet[rowIndex, 1].Format = DollarFormat;
            }

            var totalRowIndex = detailWorksheets.Length + 4;
            var lastDetailRowNum = detailWorksheets.Length + 1;

            worksheet[totalRowIndex, 0] = "Net";
            worksheet[totalRowIndex, 0].Bold = true;

            worksheet[totalRowIndex, 1] = Cell.Formula($"SUM(B$2:B${lastDetailRowNum})");
            worksheet[totalRowIndex, 1].Format = DollarFormat;

            return worksheet;
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
