using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transfer.Commands.CancelTransfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transfers.Commands;

public sealed class CancelTransferEndpoint : IEndpoint
{
	public string GroupName => TransfersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{transferId:guid}/cancel", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transfer, action: PermissionAction.Write)
			.WithSummary(summary: "Cancel a transfer and put the money back")
			.WithDescription(description:
				"Requires an Idempotency-Key header. Works both while the credit is still pending, where only " +
				"the source has to be given its money back, and after the transfer completed, where the " +
				"destination gives back what it received. Either way the operations history gains a matching " +
				"line pointing at the original, and the original is flagged as reverted. Refused once the " +
				"transfer was compensated or failed, since the money is already back or stuck, and refused " +
				"past the cancellation window. Cancelling a completed transfer needs the destination to still " +
				"hold the amount: if it was spent, nothing is undone rather than half of it. Balances appear " +
				"once the projection catches up."
			).Produces(statusCode: StatusCodes.Status202Accepted)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid transferId,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Guid? idempotencyKey = httpContext.GetIdempotencyKey();
		if (idempotencyKey is null)
			return new EmptyIdempotencyKeyException(message: "A valid 'Idempotency-Key' header is required.").ToProblem();

		CancelTransferCommand command = new CancelTransferCommand(
			UserId: currentUser.UserId,
			TransferId: transferId
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
