using FinanceTracker.Api.Endpoints.Accounts.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Account.Queries.GetAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Accounts.Queries;

public sealed class GetAccountEndpoint : IEndpoint
{
	public string GroupName => AccountsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{accountId:guid}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Account, action: PermissionAction.Read)
			.WithName(endpointName: "GetAccount")
			.WithSummary(summary: "Get an account by id")
			.WithDescription(description: "Returns an ETag header — send it back as If-Match on a PATCH to detect concurrent edits.")
			.Produces<AccountResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
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
