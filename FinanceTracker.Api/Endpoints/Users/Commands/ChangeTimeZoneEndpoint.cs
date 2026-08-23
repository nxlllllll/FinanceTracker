using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserTimeZone;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class ChangeTimeZoneEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/me/time-zone", handler: HandleAsync)
			.RequirePermission(resource: Resource.User, action: PermissionAction.Write)
			.WithSummary(summary: "Change the time zone schedules are calculated in")
			.WithDescription(description:
				"An IANA identifier such as Europe/Moscow. Recurring transactions are rescheduled into the new zone " +
				"in the same transaction, each keeping the occurrence it was already waiting for — so a move does not " +
				"skip a payment, and one already overdue stays overdue."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		ChangeTimeZoneRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<TimeZoneId, DomainException> timeZone = TimeZoneId.Create(value: request.TimeZone);
		if (timeZone.IsFailure)
			return timeZone.Error!.ToValidationProblem(fieldName: nameof(request.TimeZone));

		ChangeUserTimeZoneCommand command = new ChangeUserTimeZoneCommand(
			UserId: currentUser.UserId,
			NewTimeZone: timeZone.Value
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
