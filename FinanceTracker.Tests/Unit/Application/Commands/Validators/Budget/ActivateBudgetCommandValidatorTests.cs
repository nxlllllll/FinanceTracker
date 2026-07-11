using FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Budget;

public sealed class ActivateBudgetCommandValidatorTests
{
	private readonly ActivateBudgetCommandValidator _validator = new ActivateBudgetCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ActivateBudgetCommand command = new ActivateBudgetCommand(UserId: Guid.CreateVersion7(), BudgetId: Guid.CreateVersion7());

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ActivateBudgetCommand command = new ActivateBudgetCommand(UserId: Guid.Empty, BudgetId: Guid.CreateVersion7());

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyBudgetId_ShouldHaveError()
	{
		ActivateBudgetCommand command = new ActivateBudgetCommand(UserId: Guid.CreateVersion7(), BudgetId: Guid.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.BudgetId))).IsTrue();
	}
}
