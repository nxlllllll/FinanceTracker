using System.Net;
using FinanceTracker.Application.UseCases.User.Commands.LoginUser;
using FinanceTracker.Core.ValueObjects;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class LoginUserCommandValidatorTests
{
	private readonly LoginUserCommandValidator _validator = new LoginUserCommandValidator();

	private static LoginUserCommand ValidCommand(string password = "validPassword") => new LoginUserCommand(
		Email: Email.Create(value: "user@test.com").Value,
		Password: password,
		IpAddress: IPAddress.Loopback
	);

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		LoginUserCommand command = ValidCommand();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyEmail_ShouldHaveError()
	{
		LoginUserCommand command = ValidCommand() with { Email = default };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Email))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyPassword_ShouldHaveError()
	{
		LoginUserCommand command = ValidCommand(password: String.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Password))).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooShortPassword_ShouldHaveError()
	{
		LoginUserCommand command = ValidCommand(password: "short");

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Password))).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooLongPassword_ShouldHaveError()
	{
		LoginUserCommand command = ValidCommand(password: new String(c: 'a', count: 129));

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Password))).IsTrue();
	}

	[Test]
	public async Task Validate_WithNullIpAddress_ShouldHaveError()
	{
		LoginUserCommand command = ValidCommand() with { IpAddress = null! };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.IpAddress))).IsTrue();
	}
}
