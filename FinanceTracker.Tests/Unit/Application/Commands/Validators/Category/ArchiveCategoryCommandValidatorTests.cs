using FinanceTracker.Application.UseCases.Categories.Commands.ArchiveCategory;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Category;

public sealed class ArchiveCategoryCommandValidatorTests
{
	private readonly ArchiveCategoryCommandValidator _validator = new ArchiveCategoryCommandValidator();

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ArchiveCategoryCommand command = new ArchiveCategoryCommand(
			UserId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
	{
		ArchiveCategoryCommand command = new ArchiveCategoryCommand(
			UserId: Guid.CreateVersion7(),
			CategoryId: Guid.Empty
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.CategoryId)
		)).IsTrue();
	}
}