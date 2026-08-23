using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class RemoveRoleFromUserEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapDelete(pattern: "/{userId:guid}/roles/{roleId:guid}", handler: HandleAsync).RequireRoot()
			.WithSummary(summary: "Remove a role from a user")
			.WithDescription(description: "Fails if this would remove the last remaining root user.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid userId,
		Guid roleId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		RemoveRoleFromUserCommand command = new RemoveRoleFromUserCommand(
			UserId: userId,
			RoleId: roleId,
			RemovedBy: currentUser.UserId
		);

		Result<Unit, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
