using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Contracts.Account.Response;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Account.Queries.GetAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Account.Get;

public sealed class GetAccountEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet(pattern: "/accounts/{accountId:guid}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Account, action: PermissionAction.Read)
			.WithTags(tags: "Accounts")
			.WithSummary(summary: "Get an account by id")
			.WithDescription(description: "Returns an ETag header — send it back as If-Match on a PATCH to detect concurrent edits.")
			.Produces<AccountResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

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

		return result.ToHttpResult<AccountReadModel, AccountResponse>(
			etag: model => ETag.FromVersion(version: model.Version)
		);
	}
}
