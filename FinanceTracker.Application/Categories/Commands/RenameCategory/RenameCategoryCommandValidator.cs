using FluentValidation;

namespace FinanceTracker.Application.Categories.Commands.RenameCategory;

public sealed class RenameCategoryCommandValidator : AbstractValidator<RenameCategoryCommand>
{
	public RenameCategoryCommandValidator()
	{
		RuleFor(expression: x => x.NewName)
			.NotEmpty().WithMessage(errorMessage: "The new name cannot be empty.");
	}
}