using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.User.Queries.GetTotalBalance;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.User;
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
				"add up to one figure. Archived accounts are left out — archiving requires a zero balance, so they " +
				"contribute nothing, and including one denominated in a currency that no longer has a published rate " +
				"would fail the whole sum for the sake of adding zero. When a currency has no rate published for " +
				"today the most recent one within the staleness window is used and isApproximate comes back true; " +
				"past that window the sum is refused rather than reported as exact."
			).Produces<TotalBalanceResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status503ServiceUnavailable);
	}

	internal static async Task<IHttpResult> HandleAsync(
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<TotalBalanceReadModel, AppException> result = await sender.Send(
			request: new GetTotalBalanceQuery(UserId: currentUser.UserId),
			cancellationToken: ct
		);

		return result.ToHttpResult<TotalBalanceReadModel, TotalBalanceResponse>();
	}
}
