using FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Queries;

public sealed class GetRecurringTransactionEndpoint : IEndpoint
{
	public string GroupName => RecurringTransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{recurringTransactionId:guid}", handler: HandleAsync)
			.RequirePermission(resource: Resource.RecurringTransaction, action: PermissionAction.Read)
			.WithName(endpointName: RouteNames.GetRecurringTransaction)
			.WithSummary(summary: "Get a recurring transaction by id")
			.Produces<RecurringTransactionResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid recurringTransactionId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetRecurringTransactionQuery query = new GetRecurringTransactionQuery(
			RecurringTransactionId: recurringTransactionId,
			UserId: currentUser.UserId
		);

		Result<RecurringTransactionReadModel, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<RecurringTransactionReadModel, RecurringTransactionResponse>();
	}
}
