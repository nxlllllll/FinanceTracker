using FinanceTracker.Api.Endpoints.Transfers.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transfer.Queries.GetTransfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transfers.Queries;

public sealed class GetTransferEndpoint : IEndpoint
{
	public string GroupName => TransfersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{transferId:guid}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transfer, action: PermissionAction.Read)
			.WithName(endpointName: RouteNames.GetTransfer)
			.WithSummary(summary: "Get a transfer by id")
			.WithDescription(description:
				"The address the create endpoint points at, so a caller can follow a transfer to its outcome: " +
				"status moves from pendingCredit to completed, or to compensated when the credit could not be " +
				"applied and the money went back. A transfer belonging to someone else answers the same as one " +
				"that does not exist."
			).Produces<TransferResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid transferId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetTransferQuery query = new GetTransferQuery(
			TransferId: transferId,
			UserId: currentUser.UserId
		);

		Result<TransferReadModel, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<TransferReadModel, TransferResponse>();
	}
}
