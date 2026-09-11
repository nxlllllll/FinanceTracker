using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.UnresolvableEvent.Commands.AcknowledgeUnresolvableEvent;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.UnresolvableEvents.Commands;

public sealed class AcknowledgeUnresolvableEventEndpoint : IEndpoint
{
	public string GroupName => UnresolvableEventsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{eventId:guid}/acknowledge", handler: HandleAsync)
			.RequirePermission(resource: Resource.UnresolvableEvent, action: PermissionAction.Write)
			.WithSummary(summary: "Take an escalated event into work")
			.WithDescription(description:
				"Records that someone has picked this one up, so it stops looking untouched to whoever looks next. " +
				"Says nothing about the underlying problem — that is what resolve is for. Acknowledging twice " +
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
		AcknowledgeUnresolvableEventCommand command = new AcknowledgeUnresolvableEventCommand(
			UserId: currentUser.UserId,
			EventId: eventId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
