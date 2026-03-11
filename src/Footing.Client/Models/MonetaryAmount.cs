namespace Footing.Models;

public struct MonetaryAmount
{
    public decimal Amount { get; set; }

    public static implicit operator decimal(MonetaryAmount ma) => ma.Amount;

    public static implicit operator MonetaryAmount(decimal d) => new MonetaryAmount { Amount = d };

    public override string ToString() => $"{Amount}";

    public override int GetHashCode() => Amount.GetHashCode();

    public MonetaryAmount RoundedAmount => Math.Round(Amount, 2);

    public MonetaryAmount RoundedAbsoluteAmount => Math.Abs(RoundedAmount);

    public bool IsNegative => RoundedAmount < 0.00m;

    public string AmountDynamicHighlightCssClass => IsNegative ? AmountHighlightNegativeCssClass : AmountHighlightPositiveCssClass;

    public static string AmountSensitiveCssClass => "monetary-amount-sensitive";

    public static string AmountHighlightNegativeCssClass => "monetary-amount-highlight-negative";

    public static string AmountHighlightPositiveCssClass => "monetary-amount-highlight-positive";
}
