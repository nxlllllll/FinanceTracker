using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Account;

public sealed class CreateAccountCommandValidatorTests
{
	private ICurrencyReadRepository _currencyReadRepository = null!;
	private CreateAccountCommandValidator _validator = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_currencyReadRepository = Substitute.For<ICurrencyReadRepository>();

		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		_validator = new CreateAccountCommandValidator(
			currencyReadRepository: _currencyReadRepository,
			moneyLimits: new FakeOptionsMonitor<MoneyLimitsOptions>(value: new MoneyLimitsOptions())
		);
	}

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create();

		ValidationResult? result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
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
	public async Task Validate_WithBalanceExceedingLimit_ShouldHaveError()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(initialBalance: new MoneyLimitsOptions().MaxAmount + 0.01m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: error => error.PropertyName == nameof(command.InitialBalance)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithBalanceAtLimit_ShouldNotHaveError()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(initialBalance: new MoneyLimitsOptions().MaxAmount);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
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
	public async Task Validate_WithNonExistentCurrency_ShouldHaveError()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(currency: "XYZ");

		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(ChangeRecurringTransactionCurrencyCommand.Currency))).IsTrue();
	}
}
