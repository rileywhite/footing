namespace Footing.Models;

public class MoneyFlow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = null!;

    public MonetaryAmount Amount { get; set; }

    public Period Period { get; set; }

    public decimal GetWeeklyAmount() => Period.AsWeekly(Amount);
}
