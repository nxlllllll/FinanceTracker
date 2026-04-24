using FluentValidation;

namespace FinanceTracker.Application.Categories.Commands.UnarchiveCategory;

public sealed class UnarchiveCategoryCommandValidator : AbstractValidator<UnarchiveCategoryCommand>
{
	public UnarchiveCategoryCommandValidator()
	{
		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");
	}
}