using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class ChangeEmailEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/me/email", handler: HandleAsync)
			.RequirePermission(resource: Resource.User, action: PermissionAction.Write)
			.WithSummary(summary: "Change the current user's email")
			.WithDescription(description:
				"Requires the current password: possession of a valid token is not enough to move the address a lost " +
				"account is recovered through. Every other session is revoked, so a token taken from another device " +
				"stops working; the one used here keeps going."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status401Unauthorized)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		ChangeEmailRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeUserEmailCommand command = new ChangeUserEmailCommand(
			UserId: currentUser.UserId,
			CurrentSessionId: currentUser.SessionId,
			CurrentPassword: request.CurrentPassword,
			NewEmail: request.NewEmail
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
