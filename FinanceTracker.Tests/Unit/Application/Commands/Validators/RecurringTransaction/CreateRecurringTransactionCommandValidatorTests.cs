using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.RecurringTransaction;

public sealed class CreateRecurringTransactionCommandValidatorTests
{
    private ICurrencyReadRepository _currencyReadRepository = null!;
    private ICategoryReadRepository _categoryReadRepository = null!;
    private CreateRecurringTransactionCommandValidator _validator = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _currencyReadRepository = Substitute.For<ICurrencyReadRepository>();
        _categoryReadRepository = Substitute.For<ICategoryReadRepository>();

        _currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);
        _categoryReadRepository.ExistsAsync(
            categoryId: Arg.Any<Guid>(),
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: true);

        _validator = new CreateRecurringTransactionCommandValidator(
            currencyReadRepository: _currencyReadRepository,
            categoryReadRepository: _categoryReadRepository
        );
    }
    
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
        await Assert.That(value: result.Errors.Any(predicate: error => error.PropertyName == nameof(command.UserId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyAccountId_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(accountId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: error => error.PropertyName == nameof(command.AccountId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(categoryId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: error => error.PropertyName == nameof(command.CategoryId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithCategoryNotBelongingToUser_ShouldHaveError()
    {
        _categoryReadRepository.ExistsAsync(
            categoryId: Arg.Any<Guid>(),
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: false);

        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: error => error.PropertyName == nameof(command.CategoryId))).IsTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task Validate_WithNonPositiveAmount_ShouldHaveError(decimal amount)
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: amount);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: error => error.PropertyName == nameof(command.Amount))).IsTrue();
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
        await Assert.That(value: result.Errors.Any(predicate: error => error.PropertyName == nameof(command.DayOfMonth))).IsTrue();
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
    public async Task Validate_WithDescriptionExceeding255Chars_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(description: new string(c: 'x', count: 256));

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: error => error.PropertyName == nameof(command.Description))).IsTrue();
    }
    
    [Test]
    public async Task Validate_WithNonExistentCurrency_ShouldHaveError()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(currency: "XYZ");

        _currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(ChangeRecurringTransactionCurrencyCommand.Currency))).IsTrue();
    }
}