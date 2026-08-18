using FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Commands;

public sealed class ChangeRecurringTransactionCurrencyEndpoint : IEndpoint
{
	public string GroupName => RecurringTransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{recurringTransactionId:guid}/currency", handler: HandleAsync)
			.RequirePermission(resource: Resource.RecurringTransaction, action: PermissionAction.Write)
			.WithSummary(summary: "Change which currency a recurring transaction is denominated in")
			.WithDescription(description:
				"The figure is left as it stands — 1000 RUB becomes 1000 USD, not its equivalent. A template names an amount to charge " +
				"each month, not a value to keep constant across currencies; convert it yourself if that is what you meant."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid recurringTransactionId,
		ChangeRecurringTransactionCurrencyRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Currency, DomainException> currency = Currency.Create(value: request.Currency);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem(fieldName: nameof(request.Currency));

		ChangeRecurringTransactionCurrencyCommand command = new ChangeRecurringTransactionCurrencyCommand(
			UserId: currentUser.UserId,
			RecurringTransactionId: recurringTransactionId,
			Currency: currency.Value
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
