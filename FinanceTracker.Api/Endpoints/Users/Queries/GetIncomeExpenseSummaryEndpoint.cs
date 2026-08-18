using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Queries;

public sealed class GetIncomeExpenseSummaryEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/me/summary/{period}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Balance, action: PermissionAction.Read)
			.WithSummary(summary: "Get income and expense for a month")
			.WithDescription(description:
				"Period is any date within the month, as yyyy-MM-dd. Figures are in the user's base currency; " +
				"while a base-currency change is being applied the answer arrives with recalculationPending " +
				"set and the numbers still denominated the old way."
			).Produces<IncomeExpenseSummary>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		DateOnly period,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<IncomeExpenseSummary, AppException> result = await sender.Send(
			request: new GetIncomeExpenseSummaryQuery(UserId: currentUser.UserId, Period: period),
			cancellationToken: ct
		);

		if (result.IsFailure)
			return result.Error!.ToProblem();

		return Results.Ok(value: result.Value);
	}
}
