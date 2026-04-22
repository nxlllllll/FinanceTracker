using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Transaction;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class CreateTransactionCommandValidatorTests
{
	private readonly CreateTransactionCommandValidator _validator = new CreateTransactionCommandValidator();

    [Test]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Debit,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithZeroAmount_ShouldHaveError()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 0,
            Direction: DirectionType.Debit,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Amount)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithZeroExchangeRate_ShouldHaveError()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Debit,
            ExchangeRate: 0,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.ExchangeRate)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithFutureDate_ShouldHaveError()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Debit,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow.AddDays(value: 1)
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.OccurredAt)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithTooLongDescription_ShouldHaveError()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Debit,
            ExchangeRate: 1m,
            Description: new string(c: 'a', count: 256),
            OccurredAt: DateTime.UtcNow
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Description)
        )).IsTrue();
    }
}