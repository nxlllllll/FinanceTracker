using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.DeactivateRecurringTransaction;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.RecurringTransaction;

public sealed class DeactivateRecurringTransactionCommandValidatorTests
{
	private readonly DeactivateRecurringTransactionCommandValidator _validator = new DeactivateRecurringTransactionCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		DeactivateRecurringTransactionCommand command = new DeactivateRecurringTransactionCommand(
			UserId: Guid.CreateVersion7(),
			RecurringTransactionId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		DeactivateRecurringTransactionCommand command = new DeactivateRecurringTransactionCommand(
			UserId: Guid.Empty,
			RecurringTransactionId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyRecurringTransactionId_ShouldHaveError()
	{
		DeactivateRecurringTransactionCommand command = new DeactivateRecurringTransactionCommand(
			UserId: Guid.CreateVersion7(),
			RecurringTransactionId: Guid.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.RecurringTransactionId))).IsTrue();
	}
}
