namespace Footing.Models;

public class FootingAnalysis
{
    public MoneyFlows Inflows { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Income };
    public MoneyFlows RecurringBills { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };
    public MoneyFlows HouseholdBudgets { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };
    public MoneyFlows PersonalBudgets { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };
    public MoneyFlows EventBudgets { get; set; } = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };

    public decimal WeeklyTotalMoneyFlow =>
        Inflows.WeeklyTotalMoneyFlow -
        RecurringBills.WeeklyTotalMoneyFlow -
        HouseholdBudgets.WeeklyTotalMoneyFlow -
        PersonalBudgets.WeeklyTotalMoneyFlow -
        EventBudgets.WeeklyTotalMoneyFlow;
}
