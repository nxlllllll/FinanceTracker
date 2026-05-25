using FinanceTracker.Application.UseCases.Accounts.Commands.ArchiveAccount;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Account;

public sealed class ArchiveAccountCommandValidatorTests
{
	private readonly ArchiveAccountCommandValidator _validator = new ArchiveAccountCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ArchiveAccountCommand command = new ArchiveAccountCommand(
			UserId: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ArchiveAccountCommand command = new ArchiveAccountCommand(
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
		ArchiveAccountCommand command = new ArchiveAccountCommand(
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
