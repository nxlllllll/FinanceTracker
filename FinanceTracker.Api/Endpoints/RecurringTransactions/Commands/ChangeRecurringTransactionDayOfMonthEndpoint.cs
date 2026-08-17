using FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Commands;

public sealed class ChangeRecurringTransactionDayOfMonthEndpoint : IEndpoint
{
	public string GroupName => RecurringTransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{recurringTransactionId:guid}/day-of-month", handler: HandleAsync)
			.RequirePermission(resource: Resource.RecurringTransaction, action: PermissionAction.Write)
			.WithSummary(summary: "Move a recurring transaction to a different day")
			.WithDescription(description:
				"Day of month is 1 to 31; in a month too short for the chosen day the execution " +
				"falls on its last day. Refused on a deactivated template — reactivate it first. " +
				"Moving to a day already past this month means the next execution falls in the following one."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid recurringTransactionId,
		ChangeRecurringTransactionDayOfMonthRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeRecurringTransactionDayOfMonthCommand command = new ChangeRecurringTransactionDayOfMonthCommand(
			UserId: currentUser.UserId,
			RecurringTransactionId: recurringTransactionId,
			DayOfMonth: request.DayOfMonth
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
