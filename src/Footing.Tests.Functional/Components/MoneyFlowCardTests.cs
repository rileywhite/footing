using Bunit;
using FluentAssertions;
using Footing.Client.Components;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Functional.Components;

public class MoneyFlowCardTests : BunitContext
{
    private IRenderedComponent<MoneyFlowCard> RenderCard(
        MoneyFlows? moneyFlows = null,
        string name = "testCard",
        string label = "Test Label",
        string formPrompt = "Enter amount",
        string connectorWord = "for")
    {
        moneyFlows ??= new MoneyFlows { Direction = MoneyFlowDirection.Income };
        return Render<MoneyFlowCard>(p => p
            .Add(c => c.MoneyFlows, moneyFlows)
            .Add(c => c.Name, name)
            .Add(c => c.Label, label)
            .Add(c => c.FormPrompt, formPrompt)
            .Add(c => c.FormAmountDescriptionConnectorWord, connectorWord));
    }

    [Fact]
    public void RendersLabel() =>
        RenderCard(label: "Income").Markup.Should().Contain("Income");

    [Fact]
    public void RendersWeeklyTotal()
    {
        var flows = new MoneyFlows { Direction = MoneyFlowDirection.Income };
        flows.Add(new MoneyFlow { Name = "Salary", Amount = 100m, Period = Period.Weekly });
        var cut = RenderCard(moneyFlows: flows, label: "Income");
        cut.Markup.Should().Contain("$100");
        cut.Markup.Should().Contain("/ Week");
    }

    [Fact]
    public void RendersExistingMoneyFlows_InTable()
    {
        var flows = new MoneyFlows { Direction = MoneyFlowDirection.Income };
        flows.Add(new MoneyFlow { Name = "Salary", Amount = 2000m, Period = Period.BiWeekly });
        flows.Add(new MoneyFlow { Name = "Side Gig", Amount = 500m, Period = Period.Monthly });
        var cut = RenderCard(moneyFlows: flows);
        cut.Markup.Should().Contain("Salary");
        cut.Markup.Should().Contain("Side Gig");
    }

    [Fact]
    public void RendersFormWithPeriodOptions()
    {
        var cut = RenderCard();
        cut.Find("select").Should().NotBeNull();
        foreach (Period period in Enum.GetValues(typeof(Period)))
            cut.Markup.Should().Contain(period.ToString());
    }

    [Fact]
    public void RendersAddButton() =>
        RenderCard().Find("button[type='submit']").TextContent.Should().Contain("Add");

    [Fact]
    public void RendersRemoveIcons_ForEachFlow()
    {
        var flows = new MoneyFlows { Direction = MoneyFlowDirection.Outgo };
        flows.Add(new MoneyFlow { Name = "Bill 1", Amount = 50m, Period = Period.Monthly });
        flows.Add(new MoneyFlow { Name = "Bill 2", Amount = 75m, Period = Period.Monthly });
        RenderCard(moneyFlows: flows).FindAll(".bi-x").Count.Should().Be(2);
    }

    [Fact]
    public void RendersFormPrompt() =>
        RenderCard(formPrompt: "How much?").Markup.Should().Contain("How much?");

    [Fact]
    public void RendersConnectorWord() =>
        RenderCard(connectorWord: "from").Markup.Should().Contain("from");

    [Fact]
    public void UsesNameForHtmlIds()
    {
        var cut = RenderCard(name: "income");
        cut.Markup.Should().Contain("incomeHeading");
        cut.Markup.Should().Contain("incomeDetails");
    }

    [Fact]
    public void EmptyFlows_RendersEmptyTable() =>
        RenderCard().FindAll("table tr").Count.Should().Be(0);

    [Fact]
    public void AddButton_SubmitsValidForm_AddsMoneyFlow()
    {
        var flows = new MoneyFlows { Direction = MoneyFlowDirection.Income };
        var cut = RenderCard(moneyFlows: flows);

        cut.Find("input[placeholder='xxx.xx']").Change("1000");
        cut.Find("select").Change("Weekly");
        cut.Find("input[placeholder='Test Label Description']").Change("Test Salary");
        cut.Find("button[type='submit']").Click();

        flows.Should().HaveCount(1);
        flows[0].Name.Should().Be("Test Salary");
    }
}
