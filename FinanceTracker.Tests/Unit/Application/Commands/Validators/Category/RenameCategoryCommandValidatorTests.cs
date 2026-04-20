using FinanceTracker.Application.Categories.Commands.RenameCategory;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Category;

public sealed class RenameCategoryCommandValidatorTests
{
	private readonly RenameCategoryCommandValidator _validator = new RenameCategoryCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		RenameCategoryCommand command = new RenameCategoryCommand(
			CategoryId: Guid.NewGuid(),
			NewName: "Продукты"
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);
		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyNewName_ShouldHaveError()
	{
		RenameCategoryCommand command = new RenameCategoryCommand(
			CategoryId: Guid.NewGuid(),
			NewName: String.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.NewName)
		)).IsTrue();
	}
}