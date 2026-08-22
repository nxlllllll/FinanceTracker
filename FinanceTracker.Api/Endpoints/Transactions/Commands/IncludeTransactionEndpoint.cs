using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transaction.Commands.IncludeTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transactions.Commands;

public sealed class IncludeTransactionEndpoint : IEndpoint
{
	public string GroupName => TransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{transactionId:guid}/include", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Write)
			.WithSummary(summary: "Count a transaction in analytics again")
			.WithDescription(description:
				"Restores the amount to its category total and to any budget covering the period it falls in. " +
				"Including one that was never excluded succeeds and changes nothing."
			)
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid transactionId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		IncludeTransactionCommand command = new IncludeTransactionCommand(
			UserId: currentUser.UserId,
			TransactionId: transactionId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
