using FinanceTracker.Api.Endpoints.Accounts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Endpoints.Transactions.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transactions.Commands;

public sealed class CreateTransactionEndpoint : IEndpoint
{
	public string GroupName => AccountsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{accountId:guid}/transactions", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Write)
			.WithTags(tags: TransactionsEndpointGroup.GroupName)
			.WithSummary(summary: "Record a transaction on an account")
			.WithDescription(description:
				"Requires an Idempotency-Key header. The amount is in the account's own currency. " +
				"If no exchange rate to the base currency is published for the date yet, the transaction is still recorded and settled later — its rateStatus says so."
			).Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		CreateTransactionRequest request,
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

		CreateTransactionCommand command = new CreateTransactionCommand(
			AccountId: accountId,
			UserId: currentUser.UserId,
			CategoryId: request.CategoryId,
			Amount: request.Amount,
			Currency: currency.Value,
			Direction: request.Direction,
			Description: request.Description,
			OccurredAt: request.OccurredAt.ToUniversalTime()
		)
		{
			IdempotencyKey = idempotencyKey.Value
		};

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedAtRoute(
			linkGenerator: linkGenerator,
			httpContext: httpContext,
			routeName: RouteNames.GetTransaction,
			routeValues: transactionId => new { transactionId }
		);
	}
}
