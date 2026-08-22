using FinanceTracker.Api.Endpoints.Accounts.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Account.Queries.GetAccounts;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Accounts.Queries;

public sealed class GetAccountsEndpoint : IEndpoint
{
	public string GroupName => AccountsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: String.Empty, handler: HandleAsync)
			.RequirePermission(resource: Resource.Account, action: PermissionAction.Read)
			.WithSummary(summary: "List my accounts")
			.WithDescription(description: "Optional ?isArchived=true|false filter. Omit to return all.")
			.Produces<List<AccountResponse>>(statusCode: StatusCodes.Status200OK);
	}

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
