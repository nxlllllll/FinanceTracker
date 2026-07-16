using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.RecurringTransaction;

public sealed class ActivateRecurringTransactionCommandValidatorTests
{
	private readonly ActivateRecurringTransactionCommandValidator _validator = new ActivateRecurringTransactionCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ActivateRecurringTransactionCommand command = new ActivateRecurringTransactionCommand(
			UserId: Guid.CreateVersion7(),
			RecurringTransactionId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ActivateRecurringTransactionCommand command = new ActivateRecurringTransactionCommand(
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
		ActivateRecurringTransactionCommand command = new ActivateRecurringTransactionCommand(
			UserId: Guid.CreateVersion7(),
			RecurringTransactionId: Guid.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.RecurringTransactionId))).IsTrue();
	}
}
