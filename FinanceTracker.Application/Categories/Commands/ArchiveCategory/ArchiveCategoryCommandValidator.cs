using FluentValidation;

namespace FinanceTracker.Application.Categories.Commands.ArchiveCategory;

public sealed class ArchiveCategoryCommandValidator : AbstractValidator<ArchiveCategoryCommand>
{
	public ArchiveCategoryCommandValidator()
	{
		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");
	}
}