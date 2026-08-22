using FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransactions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Queries;

public sealed class GetRecurringTransactionsEndpoint : IEndpoint
{
	public string GroupName => RecurringTransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.RecurringTransaction, action: PermissionAction.Read)
			.WithSummary(summary: "List recurring transactions")
			.WithDescription(description:
				"Newest first, including deactivated ones. Pages forward with a cursor: send back nextCursorDate " +
				"and nextCursorId from the previous page — both or neither. Page size is 1 to 100."
			).Produces<PagedResponse<RecurringTransactionResponse>>(statusCode: StatusCodes.Status200OK)
			.ProducesValidationProblem();
	}

	internal static async Task<IHttpResult> HandleAsync(
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20)
	{
		GetRecurringTransactionsQuery query = new GetRecurringTransactionsQuery(
			UserId: currentUser.UserId,
			CursorCreatedAt: cursorCreatedAt?.ToUniversalTime(),
			CursorId: cursorId,
			PageSize: pageSize
		);

		Result<PagedResult<RecurringTransactionReadModel>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToPagedHttpResult<RecurringTransactionReadModel, RecurringTransactionResponse>();
	}
}
