using FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class ChangeUserPasswordCommandValidatorTests
{
	private readonly ChangeUserPasswordCommandValidator _validator = new ChangeUserPasswordCommandValidator();

	private const string ValidCurrentPassword = "currentPassword";

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
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
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
			NewPassword: "newPassword"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.UserId)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyCurrentSessionId_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.Empty,
			CurrentPassword: ValidCurrentPassword,
			NewPassword: "newPassword"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.CurrentSessionId)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyCurrentPassword_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: String.Empty,
			NewPassword: "newPassword"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.CurrentPassword)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooLongCurrentPassword_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: new String(c: 'a', count: 129),
			NewPassword: "newPassword"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.CurrentPassword)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyNewPassword_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
			NewPassword: String.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewPassword)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooShortNewPassword_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
			NewPassword: "short"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewPassword)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooLongNewPassword_ShouldHaveError()
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
			NewPassword: new String(c: 'a', count: 129)
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewPassword)
		)).IsTrue();
	}
}
