using FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;
using FinanceTracker.Core.ValueObjects;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Category;

public sealed class RenameCategoryCommandValidatorTests
{
	private readonly RenameCategoryCommandValidator _validator = new RenameCategoryCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		RenameCategoryCommand command = new RenameCategoryCommand(
			UserId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			NewName: Name.Create(value: "Транспорт").Value
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);
		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
	{
		RenameCategoryCommand command = new RenameCategoryCommand(
			UserId: Guid.CreateVersion7(),
			CategoryId: Guid.Empty,
			NewName: Name.Create(value: "Транспорт").Value
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.CategoryId)
		)).IsTrue();
	}
}