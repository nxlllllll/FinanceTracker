using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class ChangeUserPasswordCommandValidatorTests
{
	private readonly ChangeUserPasswordCommandValidator _validator = new ChangeUserPasswordCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			NewPassword: "newPassword"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.Empty,
			NewPassword: "newPassword"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.UserId)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyPassword_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			NewPassword: String.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewPassword)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooShortPassword_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			NewPassword: "short"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewPassword)
		)).IsTrue();
	}
}
