using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Budget;

public sealed class ChangeBudgetPeriodCommandValidatorTests
{
	private readonly ChangeBudgetPeriodCommandValidator _validator = new ChangeBudgetPeriodCommandValidator();

	private static ChangeBudgetPeriodCommand ValidCommand() => new ChangeBudgetPeriodCommand(
		UserId: Guid.CreateVersion7(),
		BudgetId: Guid.CreateVersion7(),
		From: new DateOnly(year: 2026, month: 1, day: 1),
		To: new DateOnly(year: 2026, month: 1, day: 31)
	);

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeBudgetPeriodCommand command = ValidCommand();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ChangeBudgetPeriodCommand command = ValidCommand() with { UserId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyBudgetId_ShouldHaveError()
	{
		ChangeBudgetPeriodCommand command = ValidCommand() with { BudgetId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.BudgetId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEndDateBeforeStartDate_ShouldHaveError()
	{
		ChangeBudgetPeriodCommand command = ValidCommand() with
		{
			From = new DateOnly(year: 2026, month: 1, day: 31),
			To = new DateOnly(year: 2026, month: 1, day: 1)
		};

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.To))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEndDateEqualToStartDate_ShouldHaveError()
	{
		DateOnly sameDate = new DateOnly(year: 2026, month: 1, day: 15);
		ChangeBudgetPeriodCommand command = ValidCommand() with { From = sameDate, To = sameDate };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.To))).IsTrue();
	}
}
