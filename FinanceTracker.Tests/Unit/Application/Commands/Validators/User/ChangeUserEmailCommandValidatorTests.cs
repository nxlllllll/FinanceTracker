using FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class ChangeUserEmailCommandValidatorTests
{
	private readonly ChangeUserEmailCommandValidator _validator = new ChangeUserEmailCommandValidator();

	private const string ValidCurrentPassword = "currentPassword";

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeUserEmailCommand command = new ChangeUserEmailCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
			NewEmail: "new@test.com"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ChangeUserEmailCommand command = new ChangeUserEmailCommand(
			UserId: Guid.Empty,
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
			NewEmail: "new@test.com"
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
		ChangeUserEmailCommand command = new ChangeUserEmailCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.Empty,
			CurrentPassword: ValidCurrentPassword,
			NewEmail: "new@test.com"
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
		ChangeUserEmailCommand command = new ChangeUserEmailCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: String.Empty,
			NewEmail: "new@test.com"
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
		ChangeUserEmailCommand command = new ChangeUserEmailCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: new String(c: 'a', count: 129),
			NewEmail: "new@test.com"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.CurrentPassword)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyEmail_ShouldHaveError()
	{
		ChangeUserEmailCommand command = new ChangeUserEmailCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
			NewEmail: String.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewEmail)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidEmail_ShouldHaveError()
	{
		ChangeUserEmailCommand command = new ChangeUserEmailCommand(
			UserId: Guid.CreateVersion7(),
			CurrentSessionId: Guid.CreateVersion7(),
			CurrentPassword: ValidCurrentPassword,
			NewEmail: "notanemail"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewEmail)
		)).IsTrue();
	}
}
