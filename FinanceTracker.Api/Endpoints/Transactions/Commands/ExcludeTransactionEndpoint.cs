using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transactions.Commands;

public sealed class ExcludeTransactionEndpoint : IEndpoint
{
	public string GroupName => TransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{transactionId:guid}/exclude", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Write)
			.WithSummary(summary: "Leave a transaction out of analytics")
			.WithDescription(description:
				"Withdraws the amount from its category total and from any budget it counted toward. " +
				"The account balance is untouched — the money still moved; it is only being kept out " +
				"of the numbers used to reason about spending. Excluding one that is already excluded " +
				"succeeds and changes nothing."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid transactionId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ExcludeTransactionCommand command = new ExcludeTransactionCommand(
			UserId: currentUser.UserId,
			TransactionId: transactionId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
