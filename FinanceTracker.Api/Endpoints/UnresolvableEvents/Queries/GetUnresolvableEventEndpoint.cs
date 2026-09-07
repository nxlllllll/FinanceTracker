using FinanceTracker.Api.Endpoints.UnresolvableEvents.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.UnresolvableEvent.Queries.GetUnresolvableEvent;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.UnresolvableEvents.Queries;

public sealed class GetUnresolvableEventEndpoint : IEndpoint
{
	public string GroupName => UnresolvableEventsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{eventId:guid}", handler: HandleAsync)
			.RequirePermission(resource: Resource.UnresolvableEvent, action: PermissionAction.Read)
			.WithName(endpointName: RouteNames.GetUnresolvableEvent)
			.WithSummary(summary: "Get an escalated event by id")
			.WithDescription(description:
				"Carries the payload — the message body that could not be processed — which the list leaves out. " +
				"This is what an investigation starts from: referenceId names the aggregate involved, reason says " +
				"what refused it, and payload holds what was being applied."
			).Produces<UnresolvableEventDetailResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid eventId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetUnresolvableEventQuery query = new GetUnresolvableEventQuery(
			EventId: eventId,
			UserId: currentUser.UserId
		);

		Result<UnresolvableEventDetail, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<UnresolvableEventDetail, UnresolvableEventDetailResponse>();
	}
}
