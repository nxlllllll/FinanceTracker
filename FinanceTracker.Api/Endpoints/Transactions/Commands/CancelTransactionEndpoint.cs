using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transaction.Commands.CancelTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transactions.Commands;

public sealed class CancelTransactionEndpoint : IEndpoint
{
	public string GroupName => TransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{transactionId:guid}/cancel", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Write)
			.WithSummary(summary: "Cancel a transaction and return its money")
			.WithDescription(description:
				"Requires an Idempotency-Key header. Puts the amount back on the account at the rate the " +
				"original movement was applied at, and adds a matching line to the operations history — the " +
				"original is flagged as reverted, the new one points back at it. Unlike excluding, this moves " +
				"money, so it is only allowed for a limited period after the record was entered, counted from " +
				"when it was entered rather than the date it carries. A cancelled transaction is final: it " +
				"cannot be cancelled again, excluded, re-included or re-categorised. Cancelling one that was " +
				"already excluded works and leaves the analytics alone, since it was never counted. " +
				"The returned balance appears once the projection catches up."
			).Produces(statusCode: StatusCodes.Status202Accepted)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid transactionId,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Guid? idempotencyKey = httpContext.GetIdempotencyKey();
		if (idempotencyKey is null)
			return new EmptyIdempotencyKeyException(message: "A valid 'Idempotency-Key' header is required.").ToProblem();

		CancelTransactionCommand command = new CancelTransactionCommand(
			UserId: currentUser.UserId,
			TransactionId: transactionId
		)
		{
			IdempotencyKey = idempotencyKey.Value
		};

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		if (result.IsFailure)
			return result.Error!.ToProblem();

		return Results.Accepted();
	}
}
