using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Account;

public sealed class RenameAccountCommandValidatorTests
{
	private readonly RenameAccountCommandValidator _validator = new RenameAccountCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		RenameAccountCommand command = new RenameAccountCommand(
			UserId: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			NewName: "Карта Тинькофф"
		);

		ValidationResult? result = await _validator.ValidateAsync(instance: command);
		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyNewName_ShouldHaveError()
	{
		RenameAccountCommand command = new RenameAccountCommand(
			UserId: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			NewName: String.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewName)
		)).IsTrue();
	}
}