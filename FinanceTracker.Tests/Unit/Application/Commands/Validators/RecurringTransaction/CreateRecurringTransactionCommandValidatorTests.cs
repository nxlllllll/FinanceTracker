using FinanceTracker.Application.RecurringTransactions.Commands.CreateRecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.RecurringTransaction;

public sealed class CreateRecurringTransactionCommandValidatorTests
{
    private readonly CreateRecurringTransactionCommandValidator _validator = new CreateRecurringTransactionCommandValidator();

    [Test]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        ValidationResult result = await _validator.ValidateAsync(instance: CreateRecurringTransactionCommandFactory.Create());

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyUserId_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(userId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: error => error.PropertyName == nameof(command.UserId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyAccountId_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(accountId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: error => error.PropertyName == nameof(command.AccountId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(categoryId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: error => error.PropertyName == nameof(command.CategoryId)
        )).IsTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task Validate_WithNonPositiveAmount_ShouldHaveError(decimal amount)
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: amount);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: error => error.PropertyName == nameof(command.Amount)
        )).IsTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(32)]
    [Arguments(-1)]
    public async Task Validate_WithInvalidDayOfMonth_ShouldHaveError(int day)
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: day);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: error => error.PropertyName == nameof(command.DayOfMonth)
        )).IsTrue();
    }

    [Test]
    [Arguments(1)]
    [Arguments(15)]
    [Arguments(31)]
    public async Task Validate_WithValidDayOfMonth_ShouldNotHaveErrors(int day)
    {
        ValidationResult result = await _validator.ValidateAsync(instance: CreateRecurringTransactionCommandFactory.Create(dayOfMonth: day));

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithInvalidCurrencyLength_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(currency: "US");

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: error => error.PropertyName == nameof(command.Currency)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithDescriptionExceeding255Chars_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(description: new string(c: 'x', count: 256));

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: error => error.PropertyName == nameof(command.Description)
        )).IsTrue();
    }
}