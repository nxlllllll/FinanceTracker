using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class IncludeTransactionCommandValidatorTests
{
	private readonly IncludeTransactionCommandValidator _validator = new IncludeTransactionCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		IncludeTransactionCommand command = new IncludeTransactionCommand(TransactionId: Guid.NewGuid());

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyTransactionId_ShouldHaveError()
	{
		IncludeTransactionCommand command = new IncludeTransactionCommand(TransactionId: Guid.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.TransactionId)
		)).IsTrue();
	}
}