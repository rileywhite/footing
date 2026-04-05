namespace Footing.Models;

public class MoneyFlows : List<MoneyFlow>
{
    public MoneyFlowDirection Direction { get; set; }

    public decimal WeeklyTotalMoneyFlow => this.Sum(moneyFlow => moneyFlow.GetWeeklyAmount());
}
