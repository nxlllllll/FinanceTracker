using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.User.Queries.GetTotalBalance;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Queries;

public sealed class GetTotalBalanceEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/me/balance", handler: HandleAsync)
			.RequirePermission(resource: Resource.Balance, action: PermissionAction.Read)
			.WithSummary(summary: "Get the total across all accounts")
			.WithDescription(description:
				"Converted into the user's base currency at the rates in force, so accounts in different currencies " +
				"add up to one figure. Archived accounts are included — the money in them is still theirs."
			).Produces<Money>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status503ServiceUnavailable);
	}

	internal static async Task<IHttpResult> HandleAsync(
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Money, AppException> result = await sender.Send(
			request: new GetTotalBalanceQuery(UserId: currentUser.UserId),
			cancellationToken: ct
		);

		if (result.IsFailure)
			return result.Error!.ToProblem();

		return Results.Ok(value: result.Value);
	}
}
