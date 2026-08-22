using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class ChangePasswordEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/me/password", handler: HandleAsync)
			.RequirePermission(resource: Resource.User, action: PermissionAction.Read)
			.WithSummary(summary: "Change the current user's password")
			.WithDescription(description:
				"Requires the current one — the usual defence against someone who walked up to an unlocked screen. " +
				"Every other session is revoked, which is the point: changing a password after a device is lost " +
				"has to end that device's access."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status401Unauthorized)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		ChangePasswordRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(
			UserId: currentUser.UserId,
			CurrentSessionId: currentUser.SessionId,
			CurrentPassword: request.CurrentPassword,
			NewPassword: request.NewPassword
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
