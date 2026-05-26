using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class ChangeTransactionDescriptionCommandValidatorTests
{
    private readonly ChangeTransactionDescriptionCommandValidator _validator = new ChangeTransactionDescriptionCommandValidator();

    [Test]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        ChangeTransactionDescriptionCommand command = new ChangeTransactionDescriptionCommand(
            UserId: Guid.CreateVersion7(),
            TransactionId: Guid.CreateVersion7(),
            Description: "����"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithNullDescription_ShouldNotHaveErrors()
    {
        ChangeTransactionDescriptionCommand command = new ChangeTransactionDescriptionCommand(
            UserId: Guid.CreateVersion7(),
            TransactionId: Guid.CreateVersion7(),
            Description: null
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyTransactionId_ShouldHaveError()
    {
        ChangeTransactionDescriptionCommand command = new ChangeTransactionDescriptionCommand(
            UserId: Guid.CreateVersion7(),
            TransactionId: Guid.Empty,
            Description: null
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.TransactionId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithTooLongDescription_ShouldHaveError()
    {
        ChangeTransactionDescriptionCommand command = new ChangeTransactionDescriptionCommand(
            UserId: Guid.CreateVersion7(),
            TransactionId: Guid.CreateVersion7(),
            Description: new string(c: 'a', count: 256)
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Description)
        )).IsTrue();
    }
}
