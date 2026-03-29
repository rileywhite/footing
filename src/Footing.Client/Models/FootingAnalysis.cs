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

    public bool HasAnyEntries =>
        Inflows.Count > 0 ||
        RecurringBills.Count > 0 ||
        HouseholdBudgets.Count > 0 ||
        PersonalBudgets.Count > 0 ||
        EventBudgets.Count > 0;
}
