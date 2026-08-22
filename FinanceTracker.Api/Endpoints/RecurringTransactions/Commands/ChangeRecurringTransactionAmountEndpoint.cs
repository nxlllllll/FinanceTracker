using FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Commands;

public sealed class ChangeRecurringTransactionAmountEndpoint : IEndpoint
{
	public string GroupName => RecurringTransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{recurringTransactionId:guid}/amount", handler: HandleAsync)
			.RequirePermission(resource: Resource.RecurringTransaction, action: PermissionAction.Write)
			.WithSummary(summary: "Change how much a recurring transaction is for")
			.WithDescription(description:
				"Applies from the next execution onward — transactions already produced from this template keep " +
				"the amount they were created with. Refused on a deactivated template."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid recurringTransactionId,
		ChangeRecurringTransactionAmountRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeRecurringTransactionAmountCommand command = new ChangeRecurringTransactionAmountCommand(
			UserId: currentUser.UserId,
			RecurringTransactionId: recurringTransactionId,
			Amount: request.Amount
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
