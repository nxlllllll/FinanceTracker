using FinanceTracker.Api.Endpoints.Accounts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Endpoints.Transfers.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transfers.Commands;

public sealed class CreateTransferEndpoint : IEndpoint
{
	public string GroupName => AccountsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{accountId:guid}/transfers", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transfer, action: PermissionAction.Write)
			.WithTags(tags: TransfersEndpointGroup.GroupName)
			.WithSummary(summary: "Move money to another of your accounts")
			.WithDescription(description:
				"Requires an Idempotency-Key header. The amount is in the source account's currency; if the destination " +
				"is in another, the rate in force at the time is applied. Answers 202 rather than 201 on purpose: the " +
				"debit is done, but the credit is applied by a worker and can still be reversed — if the destination " +
				"turns out to be unusable the whole transfer is compensated and the money comes back. Watch the " +
				"operations history for the outcome."
			).Produces<CreatedIdResponse>(statusCode: StatusCodes.Status202Accepted)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid accountId,
		CreateTransferRequest request,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Guid? idempotencyKey = httpContext.GetIdempotencyKey();
		if (idempotencyKey is null)
			return new EmptyIdempotencyKeyException(message: "A valid 'Idempotency-Key' header is required.").ToProblem();

		CreateTransferCommand command = new CreateTransferCommand(
			UserId: currentUser.UserId,
			FromAccountId: accountId,
			ToAccountId: request.ToAccountId,
			Amount: request.Amount,
			Description: request.Description,
			OccurredAt: request.OccurredAt.ToUniversalTime()
		)
		{
			IdempotencyKey = idempotencyKey.Value
		};

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		if (result.IsFailure)
			return result.Error!.ToProblem();

		return Results.Accepted(value: new CreatedIdResponse(Id: result.Value));
	}
}
