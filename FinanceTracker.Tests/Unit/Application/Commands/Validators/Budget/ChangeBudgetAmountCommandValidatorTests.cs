using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Budget;

public sealed class ChangeBudgetAmountCommandValidatorTests
{
	private readonly ChangeBudgetAmountCommandValidator _validator;

	public ChangeBudgetAmountCommandValidatorTests()
	{
		IOptionsMonitor<MoneyLimitsOptions> moneyLimits = Substitute.For<IOptionsMonitor<MoneyLimitsOptions>>();
		moneyLimits.CurrentValue.Returns(returnThis: new MoneyLimitsOptions { MaxAmount = 999_999_999.99m });

		_validator = new ChangeBudgetAmountCommandValidator(moneyLimits: moneyLimits);
	}

	private static ChangeBudgetAmountCommand ValidCommand(decimal amount = 100m) => new ChangeBudgetAmountCommand(
		UserId: Guid.CreateVersion7(),
		BudgetId: Guid.CreateVersion7(),
		Amount: amount
	);

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeBudgetAmountCommand command = ValidCommand();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ChangeBudgetAmountCommand command = ValidCommand() with { UserId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyBudgetId_ShouldHaveError()
	{
		ChangeBudgetAmountCommand command = ValidCommand() with { BudgetId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.BudgetId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithZeroAmount_ShouldHaveError()
	{
		ChangeBudgetAmountCommand command = ValidCommand(amount: 0m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithNegativeAmount_ShouldHaveError()
	{
		ChangeBudgetAmountCommand command = ValidCommand(amount: -1m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithAmountExceedingLimit_ShouldHaveError()
	{
		ChangeBudgetAmountCommand command = ValidCommand(amount: 1_000_000_000m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithAmountAtLimit_ShouldNotHaveError()
	{
		ChangeBudgetAmountCommand command = ValidCommand(amount: 999_999_999.99m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}
}
