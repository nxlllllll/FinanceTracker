using FinanceTracker.Api.Endpoints.Accounts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Endpoints.Transactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transaction.Queries.GetTransactions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transactions.Queries;

public sealed class GetTransactionsEndpoint : IEndpoint
{
	public string GroupName => AccountsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{accountId:guid}/transactions", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Read)
			.WithTags(tags: TransactionsEndpointGroup.GroupName)
			.WithSummary(summary: "List an account's transactions")
			.WithDescription(description:
				"Newest first. Pages forward with a cursor: send back nextCursorDate and nextCursorId from the previous page — both or neither. " +
				"Page size is 1 to 100. The direction filter takes the same values responses carry, in any casing. Dates may carry any offset; " +
				"they name an instant, not a wall clock. An account that does not exist, or belongs to someone else, answers with an empty page " +
				"rather than an error — the listing is filtered by owner, so nothing is disclosed either way."
			).Produces<PagedResponse<TransactionResponse>>(statusCode: StatusCodes.Status200OK)
			.ProducesValidationProblem();
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct,
		Guid? categoryId = null,
		string? direction = null,
		bool? isExcluded = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20)
	{
		Result<DirectionType?, ValidationException> parsedDirection = EnumQuery.ParseOptional<DirectionType>(
			value: direction,
			parameterName: nameof(direction)
		);

		if (parsedDirection.IsFailure)
			return parsedDirection.Error!.ToProblem();

		GetTransactionsQuery query = new GetTransactionsQuery(
			UserId: currentUser.UserId,
			AccountId: accountId,
			CategoryId: categoryId,
			Direction: parsedDirection.Value,
			IsExcluded: isExcluded,
			DateFrom: dateFrom?.ToUniversalTime(),
			DateTo: dateTo?.ToUniversalTime(),
			CursorOccurredAt: cursorOccurredAt?.ToUniversalTime(),
			CursorId: cursorId,
			PageSize: pageSize
		);

		Result<PagedResult<TransactionReadModel>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToPagedHttpResult<TransactionReadModel, TransactionResponse>();
	}
}
