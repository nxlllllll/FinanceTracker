using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.UnresolvableEvent.Commands.ResolveUnresolvableEvent;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.UnresolvableEvents.Commands;

public sealed class ResolveUnresolvableEventEndpoint : IEndpoint
{
	public string GroupName => UnresolvableEventsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{eventId:guid}/resolve", handler: HandleAsync)
			.RequirePermission(resource: Resource.UnresolvableEvent, action: PermissionAction.Write)
			.WithSummary(summary: "Close an escalated event")
			.WithDescription(description:
				"Marks the problem as dealt with, which is what takes it out of the pending count the alerts watch. " +
				"It records the decision only — nothing is retried or compensated on the way. Resolving twice " +
				"answers the same and keeps the first timestamp."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid eventId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ResolveUnresolvableEventCommand command = new ResolveUnresolvableEventCommand(
			UserId: currentUser.UserId,
			EventId: eventId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
