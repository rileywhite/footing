using System;

namespace Remeter.Portal.Components
{
    public struct MonetaryAmount
    {
        public decimal Amount { get; set; }

        public static implicit operator decimal(MonetaryAmount ma) => ma.Amount;

        public static implicit operator MonetaryAmount(decimal d) => new MonetaryAmount { Amount = d };

        public override string ToString() => $"{this.Amount}";

        public override int GetHashCode() => this.Amount.GetHashCode();

        public MonetaryAmount RoundedAmount => Math.Round(this.Amount, 2);

        public MonetaryAmount RoundedAbsoluteAmount => Math.Abs(this.RoundedAmount);

        public bool IsNegative => this.RoundedAmount < 0.00m;

        public string AmountDynamicHighlightCssClass => this.IsNegative ? AmountHighlightNegativeCssClass : AmountHighlightPositiveCssClass;

        public static string AmountSensitiveCssClass => "monetary-amount-sensitive";

        public static string AmountHighlightNegativeCssClass => "monetary-amount-highlight-negative";

        public static string AmountHighlightPositiveCssClass => "monetary-amount-highlight-positive";
    }
}
