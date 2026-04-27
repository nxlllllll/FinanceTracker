using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class CreateTransactionCommandValidatorTests
{
	private readonly CreateTransactionCommandValidator _validator = new CreateTransactionCommandValidator();

    [Test]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create();

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithZeroAmount_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(amount: 0);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Amount)
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
            Currency: "RUB",
            Direction: DirectionType.Debit,
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
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(description: new string(c: 'a', count: 256));

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Description)
        )).IsTrue();
    }
    
    [Test]
    public async Task Validate_WithEmptyAccountId_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(accountId: Guid.Empty);
        
        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.AccountId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyUserId_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(userId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.UserId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(categoryId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.CategoryId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithInvalidDirection_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(direction: (DirectionType)99);
        
        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Direction)
        )).IsTrue();
    }
}