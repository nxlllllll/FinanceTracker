using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class RegisterUserCommandValidatorTests
{
	private ICurrencyReadRepository _currencyReadRepository = null!;
	private RegisterUserCommandValidator _validator = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_currencyReadRepository = Substitute.For<ICurrencyReadRepository>();

		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		_validator = new RegisterUserCommandValidator(currencyReadRepository: _currencyReadRepository);
	}
	
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
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Email))).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidEmail_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(email: "notanemail");

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Email))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyPassword_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(password: String.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Password))).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooShortPassword_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(password: "short");

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Password))).IsTrue();
	}
	
	[Test]
	public async Task Validate_WithNonExistentCurrency_ShouldHaveError()
	{
		RegisterUserCommand command = RegisterUserCommandFactory.Create(baseCurrencyCode: "XYZ");
		
		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(RegisterUserCommand.BaseCurrencyCode))).IsTrue();
	}
}
