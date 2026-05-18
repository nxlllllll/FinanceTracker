using FinanceTracker.Application.UseCases.Users.Commands.RegisterUser;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class RegisterUserCommandValidatorTests
{
	private readonly RegisterUserCommandValidator _validator = new RegisterUserCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyEmail_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(email: String.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.Email)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidEmail_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(email: "notanemail");

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.Email)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyPassword_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(password: String.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.Password)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooShortPassword_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(password: "short");

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.Password)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidBaseCurrencyCode_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(baseCurrencyCode: "RU");

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.BaseCurrencyCode)
		)).IsTrue();
	}
}