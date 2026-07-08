using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.RecurringTransaction;

public sealed class ChangeRecurringTransactionDayOfMonthCommandValidatorTests
{
	private readonly ChangeRecurringTransactionDayOfMonthCommandValidator _validator = new ChangeRecurringTransactionDayOfMonthCommandValidator();

	private static ChangeRecurringTransactionDayOfMonthCommand ValidCommand(int dayOfMonth = 15) => new ChangeRecurringTransactionDayOfMonthCommand(
		UserId: Guid.CreateVersion7(),
		RecurringTransactionId: Guid.CreateVersion7(),
		DayOfMonth: dayOfMonth
	);

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeRecurringTransactionDayOfMonthCommand command = ValidCommand();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ChangeRecurringTransactionDayOfMonthCommand command = ValidCommand() with { UserId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyRecurringTransactionId_ShouldHaveError()
	{
		ChangeRecurringTransactionDayOfMonthCommand command = ValidCommand() with { RecurringTransactionId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.RecurringTransactionId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithDayOfMonthZero_ShouldHaveError()
	{
		ChangeRecurringTransactionDayOfMonthCommand command = ValidCommand(dayOfMonth: 0);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.DayOfMonth))).IsTrue();
	}

	[Test]
	public async Task Validate_WithDayOfMonth32_ShouldHaveError()
	{
		ChangeRecurringTransactionDayOfMonthCommand command = ValidCommand(dayOfMonth: 32);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.DayOfMonth))).IsTrue();
	}

	[Test]
	public async Task Validate_WithDayOfMonthAtBoundaries_ShouldNotHaveErrors()
	{
		ValidationResult resultAtOne = await _validator.ValidateAsync(instance: ValidCommand(dayOfMonth: 1));
		ValidationResult resultAt31 = await _validator.ValidateAsync(instance: ValidCommand(dayOfMonth: 31));

		await Assert.That(value: resultAtOne.IsValid).IsTrue();
		await Assert.That(value: resultAt31.IsValid).IsTrue();
	}
}
