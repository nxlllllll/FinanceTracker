using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Endpoints.Transfers.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Transfer.Queries.GetTransfers;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Transfers.Queries;

public sealed class GetTransfersEndpoint : IEndpoint
{
	public string GroupName => TransfersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transfer, action: PermissionAction.Read)
			.WithSummary(summary: "List your transfers")
			.WithDescription(description:
				"Newest first. Listed per user rather than per account, since a transfer belongs to two of them — " +
				"pass accountId to narrow it to the ones touching a given account, on either side. Pages forward " +
				"with a cursor: send back nextCursorDate and nextCursorId from the previous page — both or neither. " +
				"Page size is 1 to 100. The status filter takes the same values responses carry, in any casing. " +
				"Dates may carry any offset; they name an instant, not a wall clock."
			).Produces<PagedResponse<TransferResponse>>(statusCode: StatusCodes.Status200OK)
			.ProducesValidationProblem();
	}

	internal static async Task<IHttpResult> HandleAsync(
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct,
		Guid? accountId = null,
		string? status = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20)
	{
		Result<TransferStatus?, ValidationException> parsedStatus = EnumQuery.ParseOptional<TransferStatus>(
			value: status,
			parameterName: nameof(status)
		);

		if (parsedStatus.IsFailure)
			return parsedStatus.Error!.ToProblem();

		GetTransfersQuery query = new GetTransfersQuery(
			UserId: currentUser.UserId,
			AccountId: accountId,
			Status: parsedStatus.Value,
			DateFrom: dateFrom?.ToUniversalTime(),
			DateTo: dateTo?.ToUniversalTime(),
			CursorOccurredAt: cursorOccurredAt?.ToUniversalTime(),
			CursorId: cursorId,
			PageSize: pageSize
		);

		Result<PagedResult<TransferReadModel>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToPagedHttpResult<TransferReadModel, TransferResponse>();
	}
}
