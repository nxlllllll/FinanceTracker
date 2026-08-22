using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class AssignRoleToUserEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{userId:guid}/roles/{roleId:guid}", handler: HandleAsync).RequireRoot()
			.WithSummary(summary: "Assign a role to a user")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid userId,
		Guid roleId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		AssignRoleToUserCommand command = new AssignRoleToUserCommand(
			UserId: userId,
			RoleId: roleId,
			AssignedBy: currentUser.UserId
		);

		Result<Unit, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
