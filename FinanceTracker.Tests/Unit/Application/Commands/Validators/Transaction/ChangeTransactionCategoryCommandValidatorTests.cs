using FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionCategory;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class ChangeTransactionCategoryCommandValidatorTests
{
	private readonly ChangeTransactionCategoryCommandValidator _validator = new ChangeTransactionCategoryCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
			UserId: Guid.NewGuid(),
			TransactionId: Guid.NewGuid(),
			CategoryId: Guid.NewGuid()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyTransactionId_ShouldHaveError()
	{
		ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
			UserId: Guid.NewGuid(),
			TransactionId: Guid.Empty,
			CategoryId: Guid.NewGuid()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.TransactionId)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
	{
		ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
			UserId: Guid.NewGuid(),
			TransactionId: Guid.NewGuid(),
			CategoryId: Guid.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.CategoryId)
		)).IsTrue();
	}
}