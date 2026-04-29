using FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class ExcludeTransactionCommandValidatorTests
{
	private readonly ExcludeTransactionCommandValidator _validator = new ExcludeTransactionCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ExcludeTransactionCommand command = new ExcludeTransactionCommand(
			UserId: Guid.NewGuid(),
			TransactionId: Guid.NewGuid()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyTransactionId_ShouldHaveError()
	{
		ExcludeTransactionCommand command = new ExcludeTransactionCommand(
			UserId: Guid.NewGuid(),
			TransactionId: Guid.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.TransactionId)
		)).IsTrue();
	}
}