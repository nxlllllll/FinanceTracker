using FinanceTracker.Application.UseCases.Categories.Commands.CreateCategory;
using FinanceTracker.Core.Domains.Category;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Category;

public sealed class CreateCategoryCommandValidatorTests
{
	private readonly CreateCategoryCommandValidator _validator = new CreateCategoryCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.NewGuid(),
			Name: "Еда",
			Type: CategoryType.Expense,
			ParentId: null
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyName_ShouldHaveError()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.NewGuid(),
			Name: String.Empty,
			Type: CategoryType.Expense,
			ParentId: null
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.Name)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidType_ShouldHaveError()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.NewGuid(),
			Name: "Еда",
			Type: (CategoryType)99,
			ParentId: null
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.Type)
		)).IsTrue();
	}
	
	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.Empty,
			Name: "Еда",
			Type: CategoryType.Expense,
			ParentId: null
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.UserId)
		)).IsTrue();
	}
}