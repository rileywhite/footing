using System;
using System.Collections.Generic;
using System.Linq;

namespace Remeter.Portal.Client.Components
{
    public class MoneyFlows : List<MoneyFlow>
    {
        public MoneyFlowDirection Direction { get; set; }

        public decimal WeeklyTotalMoneyFlow => this.Sum(moneyFlow => moneyFlow.GetWeeklyAmount());
    }
}
