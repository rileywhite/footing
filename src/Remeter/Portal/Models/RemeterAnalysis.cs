using System;

namespace Remeter.Portal.Models
{
    public class RemeterAnalysis
    {
        public MoneyFlows Inflows { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Income };
        public MoneyFlows RecurringBills { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };
        public MoneyFlows HouseholdBudgets { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };
        public MoneyFlows PersonalBudgets { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };
        public MoneyFlows EventBudgets { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };

        public decimal WeeklyTotalMoneyFlow =>
            this.Inflows.WeeklyTotalMoneyFlow -
            this.RecurringBills.WeeklyTotalMoneyFlow -
            this.HouseholdBudgets.WeeklyTotalMoneyFlow -
            this.PersonalBudgets.WeeklyTotalMoneyFlow -
            this.EventBudgets.WeeklyTotalMoneyFlow;

        // TODO delete when a real version solution emerges
        public void FixVersionIssues()
        {
            this.RecurringBills.Direction = MoneyFlowDirection.Outgo;
            this.HouseholdBudgets.Direction = MoneyFlowDirection.Outgo;
            this.PersonalBudgets.Direction = MoneyFlowDirection.Outgo;
            this.EventBudgets.Direction = MoneyFlowDirection.Outgo;
        }
    }
}
