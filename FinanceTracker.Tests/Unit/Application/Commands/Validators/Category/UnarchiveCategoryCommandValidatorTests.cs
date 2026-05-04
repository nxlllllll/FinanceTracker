using FinanceTracker.Application.UseCases.Categories.Commands.UnarchiveCategory;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Category;

public sealed class UnarchiveCategoryCommandValidatorTests
{
	private readonly UnarchiveCategoryCommandValidator _validator = new UnarchiveCategoryCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(
			UserId: Guid.NewGuid(),
			CategoryId: Guid.NewGuid()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
	{
		UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(
			UserId: Guid.NewGuid(),
			CategoryId: Guid.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.CategoryId)
		)).IsTrue();
	}
}