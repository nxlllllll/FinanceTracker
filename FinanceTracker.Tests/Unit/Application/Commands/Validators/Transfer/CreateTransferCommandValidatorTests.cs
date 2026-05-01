using FinanceTracker.Application.Transfers.Commands;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transfer;

public sealed class CreateTransferCommandValidatorTests
{
    private readonly CreateTransferCommandValidator _validator = new CreateTransferCommandValidator(dateProvider: FakeDateProvider.Default);

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
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.UserId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyFromAccountId_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(fromAccountId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.FromAccountId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyToAccountId_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(toAccountId: Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.ToAccountId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithZeroAmount_ShouldHaveError()
    {
        CreateTransferCommand command = CreateTransferCommandFactory.Create(amount: 0m);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Amount)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithSameFromAndToAccountId_ShouldHaveError()
    {
        Guid accountId = Guid.NewGuid();
        CreateTransferCommand command = CreateTransferCommandFactory.Create(fromAccountId: accountId, toAccountId: accountId);

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.ToAccountId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithFutureDate_ShouldHaveError()
    {
        CreateTransferCommand command = new CreateTransferCommand(
            UserId: Guid.NewGuid(),
            FromAccountId: Guid.NewGuid(),
            CurrencyFrom: "RUB",
            ToAccountId: Guid.NewGuid(),
            CurrencyTo: "RUB",
            Amount: 500m,
            Description: null,
            OccurredAt: FakeDateProvider.Default.UtcNow.AddDays(value: 1)
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
        CreateTransferCommand command = CreateTransferCommandFactory.Create(description: new string(c: 'a', count: 256));

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Description)
        )).IsTrue();
    }
}