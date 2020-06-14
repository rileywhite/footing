using System;

namespace Remeter.Portal.Components
{
    public class RemeterAnalysis
    {
        public MoneyFlows Inflows { get; set; } = new MoneyFlows();
        public MoneyFlows RecurringBills { get; set; } = new MoneyFlows();
        public MoneyFlows HouseholdBudgets { get; set; } = new MoneyFlows();
        public MoneyFlows PersonalBudgets { get; set; } = new MoneyFlows();
        public MoneyFlows EventBudgets { get; set; } = new MoneyFlows();

        public decimal WeeklyTotalMoneyFlow =>
            this.Inflows.WeeklyTotalMoneyFlow -
            this.RecurringBills.WeeklyTotalMoneyFlow -
            this.HouseholdBudgets.WeeklyTotalMoneyFlow -
            this.PersonalBudgets.WeeklyTotalMoneyFlow -
            this.EventBudgets.WeeklyTotalMoneyFlow;
    }
}
