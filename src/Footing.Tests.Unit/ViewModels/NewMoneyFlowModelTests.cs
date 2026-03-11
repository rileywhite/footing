using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Footing.Client.ViewModels;
using Footing.Models;
using Xunit;

namespace Footing.Tests.Unit.ViewModels;

public class NewMoneyFlowModelTests
{
    private static IList<ValidationResult> ValidateModel(NewMoneyFlowModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }

    [Fact]
    public void Valid_Model_PassesValidation() =>
        ValidateModel(new() { Name = "Test", Amount = 100.00m, Period = Period.Monthly })
            .Should().BeEmpty();

    [Fact]
    public void MissingName_FailsValidation() =>
        ValidateModel(new() { Amount = 100m, Period = Period.Monthly })
            .Should().Contain(r => r.ErrorMessage!.Contains("name"));

    [Fact]
    public void MissingAmount_FailsValidation() =>
        ValidateModel(new() { Name = "Test", Period = Period.Monthly })
            .Should().Contain(r => r.ErrorMessage!.Contains("amount"));

    [Fact]
    public void MissingPeriod_FailsValidation() =>
        ValidateModel(new() { Name = "Test", Amount = 100m })
            .Should().Contain(r => r.ErrorMessage!.Contains("how often"));

    [Fact]
    public void NegativeAmount_FailsValidation() =>
        ValidateModel(new() { Name = "Test", Amount = -50m, Period = Period.Monthly })
            .Should().NotBeEmpty();

    [Fact]
    public void ZeroAmount_IsValid() =>
        ValidateModel(new() { Name = "Test", Amount = 0m, Period = Period.Monthly })
            .Should().BeEmpty();

    [Fact]
    public void WholeNumber_IsValid() =>
        ValidateModel(new() { Name = "Test", Amount = 100m, Period = Period.Monthly })
            .Should().BeEmpty();

    [Fact]
    public void TwoCents_IsValid() =>
        ValidateModel(new() { Name = "Test", Amount = 99.99m, Period = Period.Weekly })
            .Should().BeEmpty();
}
