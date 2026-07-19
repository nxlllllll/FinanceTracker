using FinanceTracker.Api.Contracts.Account.Response;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Account.Queries.GetAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Account.Get;

public sealed class GetAccountEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
		=> app.MapGet(pattern: "/api/v1/accounts/{accountId:guid}", handler: HandleAsync).RequireAuthorization();

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetAccountQuery query = new GetAccountQuery(
			AccountId: accountId,
			UserId: currentUser.UserId
		);

		Result<AccountReadModel, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<AccountReadModel, AccountResponse>();
	}
}
