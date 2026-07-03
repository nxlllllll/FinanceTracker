using FluentValidation;

namespace FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;

public sealed class RenameCategoryCommandValidator : AbstractValidator<RenameCategoryCommand>
{
	public RenameCategoryCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");
	}
}
