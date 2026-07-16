using FluentValidation;

namespace FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;

public sealed class GetOperationsHistoryQueryValidator : AbstractValidator<GetOperationsHistoryQuery>
{
	public GetOperationsHistoryQueryValidator()
	{
		RuleFor(x => x.PageSize)
			.InclusiveBetween(from: 1, to: 100).WithMessage(errorMessage: "The page size should be from 1 to 100.");

		RuleFor(x => x.CursorId).NotNull()
			.When(predicate: x => x.CursorOccurredAt is not null)
			.WithMessage(errorMessage: "CursorId must be provided together with CursorOccurredAt.");

		RuleFor(x => x.CursorOccurredAt).NotNull()
			.When(predicate: x => x.CursorId is not null)
			.WithMessage(errorMessage: "CursorOccurredAt must be provided together with CursorId.");
	}
}
