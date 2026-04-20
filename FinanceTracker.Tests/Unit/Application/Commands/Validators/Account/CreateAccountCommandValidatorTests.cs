using FinanceTracker.Application.Accounts.Commands.CreateAccount;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Account;

public sealed class CreateAccountCommandValidatorTests
{
	private readonly CreateAccountCommandValidator _validator = new CreateAccountCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		CreateAccountCommand command = new CreateAccountCommand(
			UserId: Guid.NewGuid(),
			Name: "Карта Сбер",
			AccountType: "checking",
			Currency: "RUB",
			InitialBalance: 0
		);

		ValidationResult? result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyName_ShouldHaveError()
	{
		CreateAccountCommand command = new CreateAccountCommand(
			UserId: Guid.NewGuid(),
			Name: String.Empty,
			AccountType: "checking",
			Currency: "RUB",
			InitialBalance: 0
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: error => error.PropertyName == nameof(command.Name)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithNegativeBalance_ShouldHaveError()
	{
		CreateAccountCommand command = new CreateAccountCommand(
			UserId: Guid.NewGuid(),
			Name: "Карта Сбер",
			AccountType: "checking",
			Currency: "RUB",
			InitialBalance: -1
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: error => error.PropertyName == nameof(command.InitialBalance)
		)).IsTrue();
	}
}