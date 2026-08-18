using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Commands;

public sealed class ActivateRecurringTransactionEndpoint : IEndpoint
{
	public string GroupName => RecurringTransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{recurringTransactionId:guid}/activate", handler: HandleAsync)
			.RequirePermission(resource: Resource.RecurringTransaction, action: PermissionAction.Write)
			.WithSummary(summary: "Resume a recurring transaction")
			.WithDescription(description:
				"Picks up from the next scheduled day — months that passed while it was off are not made up. " +
				"Activating one that was never deactivated succeeds and changes nothing."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid recurringTransactionId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ActivateRecurringTransactionCommand command = new ActivateRecurringTransactionCommand(
			UserId: currentUser.UserId,
			RecurringTransactionId: recurringTransactionId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
