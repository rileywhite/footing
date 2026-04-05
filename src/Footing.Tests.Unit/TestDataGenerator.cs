using Footing.Models;

namespace Footing.Tests.Unit;

public static class TestDataGenerator
{
    public static MoneyFlow CreateMoneyFlow(
        string name = "Test Item",
        decimal amount = 100m,
        Period period = Period.Weekly) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Amount = amount,
            Period = period,
        };

    public static MoneyFlows CreateMoneyFlows(
        MoneyFlowDirection direction,
        params (string Name, decimal Amount, Period Period)[] items)
    {
        var flows = new MoneyFlows { Direction = direction };
        foreach (var (name, amount, period) in items)
            flows.Add(CreateMoneyFlow(name, amount, period));
        return flows;
    }

    public static MoneyFlows CreateSampleInflows() =>
        CreateMoneyFlows(MoneyFlowDirection.Income,
            ("Salary", 2000m, Period.BiWeekly),
            ("Freelance", 500m, Period.Monthly));

    public static MoneyFlows CreateSampleRecurringBills() =>
        CreateMoneyFlows(MoneyFlowDirection.Outgo,
            ("Rent", 1500m, Period.Monthly),
            ("Phone", 80m, Period.Monthly),
            ("Internet", 60m, Period.Monthly));

    public static MoneyFlows CreateSampleHouseholdBudgets() =>
        CreateMoneyFlows(MoneyFlowDirection.Outgo,
            ("Groceries", 150m, Period.Weekly),
            ("Auto Maintenance", 200m, Period.Quarterly));

    public static MoneyFlows CreateSamplePersonalBudgets() =>
        CreateMoneyFlows(MoneyFlowDirection.Outgo,
            ("Lunch", 10m, Period.Daily),
            ("Haircut", 30m, Period.Monthly));

    public static MoneyFlows CreateSampleEventBudgets() =>
        CreateMoneyFlows(MoneyFlowDirection.Outgo,
            ("Christmas", 500m, Period.Annually),
            ("Birthday", 200m, Period.Annually));

    public static FootingAnalysis CreateSampleAnalysis() => new()
    {
        Inflows = CreateSampleInflows(),
        RecurringBills = CreateSampleRecurringBills(),
        HouseholdBudgets = CreateSampleHouseholdBudgets(),
        PersonalBudgets = CreateSamplePersonalBudgets(),
        EventBudgets = CreateSampleEventBudgets(),
    };

    public static FootingAnalysis CreateEmptyAnalysis() => new();

    public static FootingAnalysis CreatePositiveNetAnalysis() => new()
    {
        Inflows = CreateMoneyFlows(MoneyFlowDirection.Income,
            ("High Salary", 5000m, Period.BiWeekly)),
        RecurringBills = CreateMoneyFlows(MoneyFlowDirection.Outgo,
            ("Rent", 500m, Period.Monthly)),
    };

    public static FootingAnalysis CreateNegativeNetAnalysis() => new()
    {
        Inflows = CreateMoneyFlows(MoneyFlowDirection.Income,
            ("Low Salary", 500m, Period.Monthly)),
        RecurringBills = CreateMoneyFlows(MoneyFlowDirection.Outgo,
            ("Rent", 2000m, Period.Monthly)),
    };
}
