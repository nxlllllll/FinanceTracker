using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class CreateTransactionCommandValidatorTests
{
    private ICurrencyReadRepository _currencyReadRepository = null!;
    private ICategoryReadRepository _categoryReadRepository = null!;
    private CreateTransactionCommandValidator _validator = null!;

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

        _validator = new CreateTransactionCommandValidator(
            dateProvider: FakeDateProvider.Default,
            currencyReadRepository: _currencyReadRepository,
            categoryReadRepository: _categoryReadRepository,
            moneyLimits: new FakeOptionsMonitor<MoneyLimitsOptions>(value: new MoneyLimitsOptions())
        );
    }
    
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
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
    }

    [Test]
    public async Task Validate_WithAmountExceedingLimit_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(amount: new MoneyLimitsOptions().MaxAmount + 0.01m);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
    }

    [Test]
    public async Task Validate_WithAmountAtLimit_ShouldNotHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(amount: new MoneyLimitsOptions().MaxAmount);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithFutureDate_ShouldHaveError()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.CreateVersion7(),
            UserId: Guid.CreateVersion7(),
            CategoryId: Guid.CreateVersion7(),
            Amount: 1000m,
            Currency: Currency.Create(value: "RUB").Value,
            Direction: DirectionType.Debit,
            Description: null,
            OccurredAt: FakeDateProvider.Default.UtcNow.AddDays(days: 1)
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.OccurredAt))).IsTrue();
    }

    [Test]
    public async Task Validate_WithTooLongDescription_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(description: new string(c: 'a', count: 256));

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Description))).IsTrue();
    }
    
    [Test]
    public async Task Validate_WithEmptyAccountId_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(accountId: Guid.Empty);
        
        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.AccountId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyUserId_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(userId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(categoryId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.CategoryId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithCategoryNotBelongingToUser_ShouldHaveError()
    {
        _categoryReadRepository.ExistsAsync(
            categoryId: Arg.Any<Guid>(),
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: false);

        CreateTransactionCommand command = CreateTransactionCommandFactory.Create();

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.CategoryId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithInvalidDirection_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(direction: (DirectionType)99);
        
        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Direction))).IsTrue();
    }
        
    [Test]
    public async Task Validate_WithNonExistentCurrency_ShouldHaveError()
    {
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(currency: "XYZ");

        _currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(CreateTransactionCommand.Currency))).IsTrue();
    }
}