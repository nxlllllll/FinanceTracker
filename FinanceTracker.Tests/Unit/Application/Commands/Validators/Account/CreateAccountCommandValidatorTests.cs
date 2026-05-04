using FinanceTracker.Application.UseCases.Accounts.Commands.CreateAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Account;

public sealed class CreateAccountCommandValidatorTests
{
	private readonly CreateAccountCommandValidator _validator = new CreateAccountCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create();

		ValidationResult? result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyName_ShouldHaveError()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(name: String.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: error => error.PropertyName == nameof(command.Name)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithNegativeBalance_ShouldHaveError()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(initialBalance: -1);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: error => error.PropertyName == nameof(command.InitialBalance)
		)).IsTrue();
	}
	
	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(userId: Guid.Empty);
		
		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: error => error.PropertyName == nameof(command.UserId)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidType_ShouldHaveError()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(type: (AccountType)99);
		
		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: error => error.PropertyName == nameof(command.Type)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidCurrencyLength_ShouldHaveError()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(currency: "RU");

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: error => error.PropertyName == nameof(command.Currency)
		)).IsTrue();
	}
}