using FinanceTracker.Application.UseCases.Accounts.Commands.RenameAccount;
using FinanceTracker.Core.ValueObjects;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Account;

public sealed class RenameAccountCommandValidatorTests
{
	private readonly RenameAccountCommandValidator _validator = new RenameAccountCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		RenameAccountCommand command = new RenameAccountCommand(
			UserId: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			NewName: Name.Create(value: "Карта Тинькофф").Value
		);

		ValidationResult? result = await _validator.ValidateAsync(instance: command);
		await Assert.That(value: result.IsValid).IsTrue();
	}
	
	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		RenameAccountCommand command = new RenameAccountCommand(
			UserId: Guid.Empty,
			AccountId: Guid.CreateVersion7(),
			NewName: Name.Create(value: "Карта Тинькофф").Value
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
		RenameAccountCommand command = new RenameAccountCommand(
			UserId: Guid.CreateVersion7(),
			AccountId: Guid.Empty,
			NewName: Name.Create(value: "Карта Тинькофф").Value
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.AccountId)
		)).IsTrue();
	}
}