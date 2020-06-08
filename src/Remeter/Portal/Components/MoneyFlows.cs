using System;
using System.Collections.Generic;
using System.Linq;

namespace Remeter.Portal.Components
{
    public class MoneyFlows
    {
        public bool HasFlows => this.HasInflows || this.HasOutflows;

        public bool IsEmpty => !this.HasFlows;

        public bool HasInflows => this.Inflows.Any();

        public decimal WeeklyTotalInflow => this.Inflows.Sum(inflow => inflow.GetWeeklyAmount());

        public decimal WeeklyTotalOutflow => this.Outflows.Sum(outflow => outflow.GetWeeklyAmount());

        public bool HasOutflows => this.Outflows.Any();

        public List<Inflow> Inflows = new List<Inflow>();
        public List<Outflow> Outflows = new List<Outflow>();

        public enum Period
        {
            Weekly,
            BiWeekly,
            Monthly,
            SemiMonthly,
        }

        public struct Inflow
        {
            public string Name { get; set; }

            public decimal Amount { get; set; }

            public Period Period { get; set; }

            public decimal GetWeeklyAmount() => this.Period.AsWeekly(this.Amount);
        }

        public struct Outflow
        {
            public string Name { get; set; }

            public decimal Amount { get; set; }

            public Period Period { get; set; }

            public decimal GetWeeklyAmount() => this.Period.AsWeekly(this.Amount);
        }
    }
}