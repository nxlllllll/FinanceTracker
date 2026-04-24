using FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class ChangeUserBaseCurrencyCommandValidatorTests
{
    private readonly ChangeUserBaseCurrencyCommandValidator _validator = new ChangeUserBaseCurrencyCommandValidator();

    [Test]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(
            UserId: Guid.NewGuid(),
            NewBaseCurrency: "USD"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyUserId_ShouldHaveError()
    {
        ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(
            UserId: Guid.Empty,
            NewBaseCurrency: "USD"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.UserId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyNewBaseCurrency_ShouldHaveError()
    {
        ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(
            UserId: Guid.NewGuid(),
            NewBaseCurrency: String.Empty
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.NewBaseCurrency)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithInvalidCurrencyLength_ShouldHaveError()
    {
        ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(
            UserId: Guid.NewGuid(),
            NewBaseCurrency: "RU"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.NewBaseCurrency)
        )).IsTrue();
    }
}