using FluentValidation;

namespace FinanceTracker.Application.UseCases.Categories.Commands.RenameCategory;

public sealed class RenameCategoryCommandValidator : AbstractValidator<RenameCategoryCommand>
{
	public RenameCategoryCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");
		
		RuleFor(expression: command => command.CategoryId)
			.NotEmpty().WithMessage(errorMessage: "The category cannot be empty.");
		
		RuleFor(expression: command => command.NewName)
			.NotEmpty().WithMessage(errorMessage: "The new name cannot be empty.");
	}
}