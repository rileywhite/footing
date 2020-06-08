using System;
using System.Collections.Generic;

namespace Remeter.Portal.Components
{
    public class MoneyFlows
    {
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
        }
    }
}