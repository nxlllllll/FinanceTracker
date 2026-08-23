using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Operation;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Queries;

public sealed class GetOperationsHistoryEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/me/operations", handler: HandleAsync)
			.RequirePermission(resource: Resource.Transaction, action: PermissionAction.Read)
			.WithSummary(summary: "List every movement of money, newest first")
			.WithDescription(description:
				"Transactions and transfers in one stream, ordered by when they happened — which is what separates this " +
				"from listing either on its own. Exactly one of the transaction or transfer blocks is filled, and type " +
				"says which. Pages forward with a cursor: send back nextCursorDate and nextCursorId — both or neither. " +
				"The type filter takes the same values responses carry, in any casing. Dates may carry any offset; " +
				"they name an instant."
			).Produces<PagedResponse<OperationResponse>>(statusCode: StatusCodes.Status200OK)
			.ProducesValidationProblem();
	}

	internal static async Task<IHttpResult> HandleAsync(
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct,
		string? type = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20)
	{
		Result<OperationFilterType?, ValidationException> parsedType = EnumQuery.ParseOptional<OperationFilterType>(
			value: type,
			parameterName: nameof(type)
		);

		if (parsedType.IsFailure)
			return parsedType.Error!.ToProblem();

		GetOperationsHistoryQuery query = new GetOperationsHistoryQuery(
			UserId: currentUser.UserId,
			Type: parsedType.Value,
			DateFrom: dateFrom?.ToUniversalTime(),
			DateTo: dateTo?.ToUniversalTime(),
			CursorOccurredAt: cursorOccurredAt?.ToUniversalTime(),
			CursorId: cursorId,
			PageSize: pageSize
		);

		Result<PagedResult<Operation>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToPagedHttpResult<Operation, OperationResponse>();
	}
}
