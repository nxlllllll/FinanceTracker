using FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Budget;

public sealed class DeactivateBudgetCommandValidatorTests
{
	private readonly DeactivateBudgetCommandValidator _validator = new DeactivateBudgetCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		DeactivateBudgetCommand command = new DeactivateBudgetCommand(UserId: Guid.CreateVersion7(), BudgetId: Guid.CreateVersion7());

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		DeactivateBudgetCommand command = new DeactivateBudgetCommand(UserId: Guid.Empty, BudgetId: Guid.CreateVersion7());

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyBudgetId_ShouldHaveError()
	{
		DeactivateBudgetCommand command = new DeactivateBudgetCommand(UserId: Guid.CreateVersion7(), BudgetId: Guid.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.BudgetId))).IsTrue();
	}
}
