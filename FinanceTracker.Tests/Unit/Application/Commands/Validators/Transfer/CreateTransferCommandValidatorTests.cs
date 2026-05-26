using FinanceTracker.Application.UseCases.Transfer.Commands;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transfer;

public sealed class CreateTransferCommandValidatorTests
{
    private ICurrencyReadRepository _currencyReadRepository = null!;
    private CreateTransferCommandValidator _validator = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _currencyReadRepository = Substitute.For<ICurrencyReadRepository>();

        _currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

        _validator = new CreateTransferCommandValidator(
            dateProvider: FakeDateProvider.Default,
            currencyReadRepository: _currencyReadRepository
        );
    }
    
    [Test]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create();

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyUserId_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(userId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyFromAccountId_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(fromAccountId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.FromAccountId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyToAccountId_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(toAccountId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.ToAccountId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithZeroAmount_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(amount: 0m);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
    }

    [Test]
    public async Task Validate_WithSameFromAndToAccountId_ShouldHaveError()
    {
        Guid accountId = Guid.CreateVersion7();
        CreateTransferCommand command = CreateTransferCommandFactory.Create(fromAccountId: accountId, toAccountId: accountId);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.ToAccountId))).IsTrue();
    }

    [Test]
    public async Task Validate_WithFutureDate_ShouldHaveError()
    {
        CreateTransferCommand command = new CreateTransferCommand(
            UserId: Guid.CreateVersion7(),
            FromAccountId: Guid.CreateVersion7(),
            CurrencyFrom: Currency.Create(value: "RUB").Value,
            ToAccountId: Guid.CreateVersion7(),
            CurrencyTo: Currency.Create(value: "RUB").Value,
            Amount: 500m,
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
        CreateTransferCommand command = CreateTransferCommandFactory.Create(description: new string(c: 'a', count: 256));

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Description))).IsTrue();
    }
            
    [Test]
    public async Task Validate_WithNonExistentCurrencyFrom_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(currencyFrom: "XYZ");

        _currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(CreateTransferCommand.CurrencyFrom))).IsTrue();
    }
            
    [Test]
    public async Task Validate_WithNonExistentCurrencyTo_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(currencyTo: "XYZ");

        _currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(CreateTransferCommand.CurrencyTo))).IsTrue();
    }
}
