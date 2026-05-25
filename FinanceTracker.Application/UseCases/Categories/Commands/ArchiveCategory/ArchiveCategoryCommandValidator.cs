using FluentValidation;

namespace FinanceTracker.Application.UseCases.Categories.Commands.ArchiveCategory;

public sealed class ArchiveCategoryCommandValidator : AbstractValidator<ArchiveCategoryCommand>
{
	public ArchiveCategoryCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");
	}
}
