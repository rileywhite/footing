using Nito.AsyncEx;
using Simplexcel;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Remeter.Portal.Components
{
    public class SimplexcelExporter
    {
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

            var cell = new Cell(CellType.Text);
            cell.Value = "hullo wurld";

            var worksheet = new Worksheet("Test");
            worksheet[0, 0] = cell;

            var workbook = new Workbook();
            workbook.Add(worksheet);

            return workbook;
        }

        public async Task ExportTo(Stream stream) => (await this.Workbook).Save(stream);
    }
}
