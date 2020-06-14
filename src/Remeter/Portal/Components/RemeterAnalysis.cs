using System;
using System.Collections.Generic;
using System.Linq;

namespace Remeter.Portal.Components
{
    public class RemeterAnalysis
    {
        public MoneyFlows Inflows { get; } = new MoneyFlows();
        public MoneyFlows Outflows { get; } = new MoneyFlows();

        public bool HasFlows => this.HasInflows || this.HasOutflows;

        public bool IsEmpty => !this.HasFlows;

        public bool HasInflows => this.Inflows.Any();

        public decimal WeeklyTotalInflow => this.Inflows.WeeklyTotalMoneyFlow;

        public decimal WeeklyTotalOutflow => this.Outflows.WeeklyTotalMoneyFlow;

        public bool HasOutflows => this.Outflows.Any();
    }
}
