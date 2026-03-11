using Bunit;
using FluentAssertions;
using Footing.Client.Components;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Functional.Components;

public class MonetaryAmountDisplayTests : BunitContext
{
    [Fact]
    public void RendersPositiveAmount_WithDollarSign()
    {
        var cut = Render<MonetaryAmountDisplay>(p =>
            p.Add(c => c.Amount, (MonetaryAmount)100.50m));
        cut.Markup.Should().Contain("$100.50");
        cut.Markup.Should().NotContain("-$");
    }

    [Fact]
    public void RendersNegativeAmount_WithNegativeSign()
    {
        var cut = Render<MonetaryAmountDisplay>(p =>
            p.Add(c => c.Amount, (MonetaryAmount)(-50.75m)));
        cut.Markup.Should().Contain("-$50.75");
    }

    [Fact]
    public void RendersZeroAmount_WithDollarSign()
    {
        var cut = Render<MonetaryAmountDisplay>(p =>
            p.Add(c => c.Amount, (MonetaryAmount)0m));
        cut.Markup.Should().Contain("$0");
        cut.Markup.Should().NotContain("-$");
    }

    [Fact]
    public void RoundsToTwoDecimalPlaces()
    {
        var cut = Render<MonetaryAmountDisplay>(p =>
            p.Add(c => c.Amount, (MonetaryAmount)99.999m));
        cut.Markup.Should().Contain("$100.00");
    }

    [Fact]
    public void HasHighlightSpan()
    {
        var cut = Render<MonetaryAmountDisplay>(p =>
            p.Add(c => c.Amount, (MonetaryAmount)50m));
        cut.Find("span").ClassList.Should().Contain("monetary-amount-highlight");
    }
}
