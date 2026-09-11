using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Endpoints.UnresolvableEvents.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.UnresolvableEvent.Queries.GetUnresolvableEvents;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using UnresolvableEventReadModel = FinanceTracker.Core.ReadModels.UnresolvableEvent.UnresolvableEvent;

namespace FinanceTracker.Api.Endpoints.UnresolvableEvents.Queries;

public sealed class GetUnresolvableEventsEndpoint : IEndpoint
{
	public string GroupName => UnresolvableEventsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.UnresolvableEvent, action: PermissionAction.Read)
			.WithSummary(summary: "List events escalated for manual resolution")
			.WithDescription(description:
				"The operations queue: everything the system could not fix on its own and will not retry. " +
				"Oldest first, unlike the user-facing lists — a backlog is worked from its head. " +
				"Pages forward with a cursor: send back nextCursorDate and nextCursorId from the previous page — " +
				"both or neither. Page size is 1 to 100. The type filter takes the same values responses carry, " +
				"in any casing. isAcknowledged and isResolved are independent and may be combined."
			).Produces<PagedResponse<UnresolvableEventResponse>>(statusCode: StatusCodes.Status200OK)
			.ProducesValidationProblem();
	}

	internal static async Task<IHttpResult> HandleAsync(
		ISender sender,
		ICurrentUserProvider currentUser,
		CancellationToken ct,
		string? type = null,
		bool? isAcknowledged = null,
		bool? isResolved = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20)
	{
		Result<UnresolvableEventType?, ValidationException> parsedType = EnumQuery.ParseOptional<UnresolvableEventType>(
			value: type,
			parameterName: nameof(type)
		);

		if (parsedType.IsFailure)
			return parsedType.Error!.ToProblem();

		GetUnresolvableEventsQuery query = new GetUnresolvableEventsQuery(
			UserId: currentUser.UserId,
			Type: parsedType.Value,
			IsAcknowledged: isAcknowledged,
			IsResolved: isResolved,
			CursorOccurredAt: cursorOccurredAt?.ToUniversalTime(),
			CursorId: cursorId,
			PageSize: pageSize
		);

		Result<PagedResult<UnresolvableEventReadModel>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToPagedHttpResult<UnresolvableEventReadModel, UnresolvableEventResponse>();
	}
}
