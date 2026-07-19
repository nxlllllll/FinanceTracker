using FinanceTracker.Api.Contracts.Account.Response;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.User.Queries.GetAccounts;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Account.Get;

public sealed class GetAccountsEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
		=> app.MapGet(pattern: "/api/v1/accounts", handler: HandleAsync).RequireAuthorization();

	private static async Task<IHttpResult> HandleAsync(
		bool? isArchived,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetAccountsQuery query = new GetAccountsQuery(
			UserId: currentUser.UserId,
			IsArchived: isArchived
		);

		Result<IReadOnlyList<AccountReadModel>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<AccountReadModel, AccountResponse>();
	}
}
