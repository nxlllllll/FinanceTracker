using FluentValidation;

namespace FinanceTracker.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
	public CreateCategoryCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.Name)
			.NotEmpty().WithMessage(errorMessage: "The name cannot be empty.");

		RuleFor(expression: command => command.Type)
			.IsInEnum().WithMessage(errorMessage: "The category type can only be 'Income' or 'Expense'.");
	}
}