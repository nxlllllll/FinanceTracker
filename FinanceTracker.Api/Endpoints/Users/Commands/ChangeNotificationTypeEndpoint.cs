using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserNotificationType;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class ChangeNotificationTypeEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/me/notifications", handler: HandleAsync)
			.RequirePermission(resource: Resource.User, action: PermissionAction.Write)
			.WithSummary(summary: "Choose how you are notified, or turn notifications off")
			.WithDescription(description:
				"Notifications tell you about events on your own data. " +
				"\"email\" sends them to the address you sign in with; null turns them off."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		ChangeNotificationTypeRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeUserNotificationTypeCommand command = new ChangeUserNotificationTypeCommand(
			UserId: currentUser.UserId,
			NewNotificationType: request.Type
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
