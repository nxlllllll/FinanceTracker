using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.RecurringTransaction;

public sealed class ChangeRecurringTransactionAmountCommandValidatorTests
{
	private readonly ChangeRecurringTransactionAmountCommandValidator _validator;

	public ChangeRecurringTransactionAmountCommandValidatorTests()
	{
		IOptionsMonitor<MoneyLimitsOptions> moneyLimits = Substitute.For<IOptionsMonitor<MoneyLimitsOptions>>();
		moneyLimits.CurrentValue.Returns(returnThis: new MoneyLimitsOptions { MaxAmount = 999_999_999.99m });

		_validator = new ChangeRecurringTransactionAmountCommandValidator(moneyLimits: moneyLimits);
	}

	private static ChangeRecurringTransactionAmountCommand ValidCommand(decimal amount = 100m) => new ChangeRecurringTransactionAmountCommand(
		UserId: Guid.CreateVersion7(),
		RecurringTransactionId: Guid.CreateVersion7(),
		Amount: amount
	);

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeRecurringTransactionAmountCommand command = ValidCommand();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ChangeRecurringTransactionAmountCommand command = ValidCommand() with { UserId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyRecurringTransactionId_ShouldHaveError()
	{
		ChangeRecurringTransactionAmountCommand command = ValidCommand() with { RecurringTransactionId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.RecurringTransactionId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithZeroAmount_ShouldHaveError()
	{
		ChangeRecurringTransactionAmountCommand command = ValidCommand(amount: 0m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithNegativeAmount_ShouldHaveError()
	{
		ChangeRecurringTransactionAmountCommand command = ValidCommand(amount: -1m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithAmountExceedingLimit_ShouldHaveError()
	{
		ChangeRecurringTransactionAmountCommand command = ValidCommand(amount: 1_000_000_000m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithAmountAtLimit_ShouldNotHaveError()
	{
		ChangeRecurringTransactionAmountCommand command = ValidCommand(amount: 999_999_999.99m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}
}
