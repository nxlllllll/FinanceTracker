using FluentValidation;

namespace FinanceTracker.Application.UseCases.Categories.Queries.GetCategories;

public sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>
{
	public GetCategoriesQueryValidator()
	{
		RuleFor(x => x.PageSize)
			.InclusiveBetween(from: 1, to: 100).WithMessage(errorMessage: "The page size should be from 1 to 100.");
 
		RuleFor(x => x.CursorId).NotNull()
			.When(predicate: x => x.CursorCreatedAt is not null)
			.WithMessage(errorMessage: "CursorId must be provided together with CursorCreatedAt.");
 
		RuleFor(x => x.CursorCreatedAt).NotNull()
			.When(predicate: x => x.CursorId is not null)
			.WithMessage(errorMessage: "CursorCreatedAt must be provided together with CursorId.");
	}
}