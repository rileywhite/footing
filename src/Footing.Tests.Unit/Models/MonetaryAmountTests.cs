using FluentAssertions;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Unit.Models;

public class MonetaryAmountTests
{
    [Fact]
    public void ImplicitConversion_FromDecimal_Works()
    {
        MonetaryAmount amount = 42.50m;
        amount.Amount.Should().Be(42.50m);
    }

    [Fact]
    public void ImplicitConversion_ToDecimal_Works()
    {
        var amount = new MonetaryAmount { Amount = 42.50m };
        decimal result = amount;
        result.Should().Be(42.50m);
    }

    [Fact]
    public void RoundedAmount_RoundsToTwoDecimalPlaces()
    {
        MonetaryAmount amount = 10.456m;
        amount.RoundedAmount.Amount.Should().Be(10.46m);
    }

    [Fact]
    public void RoundedAmount_AlreadyRounded_Unchanged()
    {
        MonetaryAmount amount = 10.45m;
        amount.RoundedAmount.Amount.Should().Be(10.45m);
    }

    [Fact]
    public void RoundedAbsoluteAmount_NegativeValue_ReturnsPositive()
    {
        MonetaryAmount amount = -42.567m;
        amount.RoundedAbsoluteAmount.Amount.Should().Be(42.57m);
    }

    [Fact]
    public void RoundedAbsoluteAmount_PositiveValue_ReturnsPositive()
    {
        MonetaryAmount amount = 42.567m;
        amount.RoundedAbsoluteAmount.Amount.Should().Be(42.57m);
    }

    [Fact]
    public void IsNegative_NegativeValue_ReturnsTrue()
    {
        MonetaryAmount amount = -1m;
        amount.IsNegative.Should().BeTrue();
    }

    [Fact]
    public void IsNegative_PositiveValue_ReturnsFalse()
    {
        MonetaryAmount amount = 1m;
        amount.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void IsNegative_Zero_ReturnsFalse()
    {
        MonetaryAmount amount = 0m;
        amount.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void IsNegative_VerySmallNegative_RoundsToZero_ReturnsFalse()
    {
        MonetaryAmount amount = -0.004m;
        amount.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void AmountDynamicHighlightCssClass_Negative_ReturnsNegativeClass()
    {
        MonetaryAmount amount = -50m;
        amount.AmountDynamicHighlightCssClass.Should().Be("monetary-amount-highlight-negative");
    }

    [Fact]
    public void AmountDynamicHighlightCssClass_Positive_ReturnsPositiveClass()
    {
        MonetaryAmount amount = 50m;
        amount.AmountDynamicHighlightCssClass.Should().Be("monetary-amount-highlight-positive");
    }

    [Fact]
    public void AmountSensitiveCssClass_ReturnsExpected() =>
        MonetaryAmount.AmountSensitiveCssClass.Should().Be("monetary-amount-sensitive");

    [Fact]
    public void ToString_ReturnsAmountString()
    {
        MonetaryAmount amount = 42.50m;
        amount.ToString().Should().Be("42.50");
    }

    [Fact]
    public void GetHashCode_SameAmount_SameHash()
    {
        MonetaryAmount a = 42.50m;
        MonetaryAmount b = 42.50m;
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
