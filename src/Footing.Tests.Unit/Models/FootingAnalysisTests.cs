using FluentAssertions;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Unit.Models;

public class FootingAnalysisTests
{
    [Fact]
    public void WeeklyTotalMoneyFlow_Empty_ReturnsZero() =>
        TestDataGenerator.CreateEmptyAnalysis().WeeklyTotalMoneyFlow.Should().Be(0m);

    [Fact]
    public void WeeklyTotalMoneyFlow_IncomeOnly_ReturnsPositive()
    {
        var analysis = new FootingAnalysis
        {
            Inflows = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Income,
                ("Salary", 1000m, Period.Weekly)),
        };
        analysis.WeeklyTotalMoneyFlow.Should().Be(1000m);
    }

    [Fact]
    public void WeeklyTotalMoneyFlow_SubtractsAllOutgoCategories()
    {
        var analysis = new FootingAnalysis
        {
            Inflows = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Income,
                ("Income", 1000m, Period.Weekly)),
            RecurringBills = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Outgo,
                ("Bill", 100m, Period.Weekly)),
            HouseholdBudgets = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Outgo,
                ("Groceries", 200m, Period.Weekly)),
            PersonalBudgets = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Outgo,
                ("Lunch", 50m, Period.Weekly)),
            EventBudgets = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Outgo,
                ("Birthday", 25m, Period.Weekly)),
        };
        analysis.WeeklyTotalMoneyFlow.Should().Be(625m);
    }

    [Fact]
    public void WeeklyTotalMoneyFlow_NegativeResult_WhenExpensesExceedIncome() =>
        TestDataGenerator.CreateNegativeNetAnalysis().WeeklyTotalMoneyFlow.Should().BeNegative();

    [Fact]
    public void WeeklyTotalMoneyFlow_PositiveResult_WhenIncomeExceedsExpenses() =>
        TestDataGenerator.CreatePositiveNetAnalysis().WeeklyTotalMoneyFlow.Should().BePositive();

    [Fact]
    public void NewAnalysis_HasCorrectDirections()
    {
        var analysis = new FootingAnalysis();
        analysis.Inflows.Direction.Should().Be(MoneyFlowDirection.Income);
        analysis.RecurringBills.Direction.Should().Be(MoneyFlowDirection.Outgo);
        analysis.HouseholdBudgets.Direction.Should().Be(MoneyFlowDirection.Outgo);
        analysis.PersonalBudgets.Direction.Should().Be(MoneyFlowDirection.Outgo);
        analysis.EventBudgets.Direction.Should().Be(MoneyFlowDirection.Outgo);
    }

    [Fact]
    public void WeeklyTotalMoneyFlow_MixedPeriods_CalculatesCorrectly()
    {
        var analysis = new FootingAnalysis
        {
            Inflows = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Income,
                ("Salary", 2000m, Period.BiWeekly)),
            RecurringBills = TestDataGenerator.CreateMoneyFlows(MoneyFlowDirection.Outgo,
                ("Rent", 1200m, Period.Monthly)),
        };
        var expectedIncome = 2000m / 2m;
        var expectedBills = 1200m * 12m / 52m;
        analysis.WeeklyTotalMoneyFlow.Should().Be(expectedIncome - expectedBills);
    }
}
