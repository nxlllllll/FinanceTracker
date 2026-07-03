using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Core.Repositories.Category;
using FluentValidation;

namespace FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
	public CreateCategoryCommandValidator(ICategoryReadRepository categoryReadRepository)
	{
		RuleFor(expression: command => command.UserId)
		.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.Type)
		.IsInEnum().WithMessage(errorMessage: "The category type can only be 'Income' or 'Expense'.");

		RuleFor(expression: command => command.ParentId).MustBelongToUserWhenSpecified(
			existsForUserAsync: categoryReadRepository.ExistsAsync,
			userIdSelector: command => command.UserId,
			entityName: "parent category"
		);
	}
}
