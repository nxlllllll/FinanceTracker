using FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ValueObjects;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Category;

public sealed class CreateCategoryCommandValidatorTests
{
	private readonly CreateCategoryCommandValidator _validator = new CreateCategoryCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "���").Value,
			Type: CategoryType.Expense,
			ParentId: null
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidType_ShouldHaveError()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "���").Value,
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
			Name: Name.Create(value: "���").Value,
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
