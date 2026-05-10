using FinanceTracker.Application.UseCases.Accounts.Commands.UnarchiveAccount;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Account;

public sealed class UnarchiveAccountCommandValidatorTests
{
	private readonly UnarchiveAccountCommandValidator _validator = new UnarchiveAccountCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		UnarchiveAccountCommand command = new UnarchiveAccountCommand(
			UserId: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		UnarchiveAccountCommand command = new UnarchiveAccountCommand(
			UserId: Guid.Empty,
			AccountId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.UserId)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyAccountId_ShouldHaveError()
	{
		UnarchiveAccountCommand command = new UnarchiveAccountCommand(
			UserId: Guid.CreateVersion7(),
			AccountId: Guid.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.AccountId)
		)).IsTrue();
	}
}