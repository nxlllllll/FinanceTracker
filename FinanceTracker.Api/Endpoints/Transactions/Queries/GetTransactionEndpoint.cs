using FinanceTracker.Api.Endpoints.Transactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transactions.Queries;

public sealed class GetTransactionEndpoint : IEndpoint
{
	public string GroupName => TransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{transactionId:guid}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Read)
			.WithName(endpointName: RouteNames.GetTransaction)
			.WithSummary(summary: "Get a transaction by id")
			.WithDescription(description:
				"Addressed without its account: a transaction id identifies it on its own, and one that " +
				"belongs to someone else answers the same as one that does not exist."
			).Produces<TransactionResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid transactionId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetTransactionQuery query = new GetTransactionQuery(
			TransactionId: transactionId,
			UserId: currentUser.UserId
		);

		Result<TransactionReadModel, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<TransactionReadModel, TransactionResponse>();
	}
}
