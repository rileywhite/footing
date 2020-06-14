using System;
using System.Collections.Generic;
using System.Linq;

namespace Remeter.Portal.Components
{
    public class MoneyFlows : List<MoneyFlow>
    {
        public decimal WeeklyTotalMoneyFlow => this.Sum(moneyFlow => moneyFlow.GetWeeklyAmount());
    }
}
