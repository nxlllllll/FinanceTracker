using FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Commands;

public sealed class CreateRecurringTransactionEndpoint : IEndpoint
{
	public string GroupName => RecurringTransactionsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.RecurringTransaction, action: PermissionAction.Write)
			.WithSummary(summary: "Schedule a transaction to repeat monthly")
			.WithDescription(description:
				"Requires an Idempotency-Key header. Creates a template, not a transaction: a worker turns " +
				"it into a real one on the given day each month. Day of month is 1 to 31; in a month too " +
				"short for the chosen day the execution falls on its last day, so no month is ever skipped"
			).Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		CreateRecurringTransactionRequest request,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		LinkGenerator linkGenerator,
		ISender sender,
		CancellationToken ct)
	{
		Guid? idempotencyKey = httpContext.GetIdempotencyKey();
		if (idempotencyKey is null)
			return new EmptyIdempotencyKeyException(message: "A valid 'Idempotency-Key' header is required.").ToProblem();

		Result<Currency, DomainException> currency = Currency.Create(value: request.Currency);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem(fieldName: nameof(request.Currency));

		CreateRecurringTransactionCommand command = new CreateRecurringTransactionCommand(
			UserId: currentUser.UserId,
			AccountId: request.AccountId,
			CategoryId: request.CategoryId,
			Amount: request.Amount,
			Currency: currency.Value,
			Direction: request.Direction,
			DayOfMonth: request.DayOfMonth,
			Description: request.Description
		)
		{
			IdempotencyKey = idempotencyKey.Value
		};

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedAtRoute(
			linkGenerator: linkGenerator,
			httpContext: httpContext,
			routeName: RouteNames.GetRecurringTransaction,
			routeValues: recurringTransactionId => new { recurringTransactionId }
		);
	}
}
