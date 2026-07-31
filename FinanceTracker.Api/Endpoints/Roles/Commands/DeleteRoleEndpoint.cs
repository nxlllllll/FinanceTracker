using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Commands.DeleteRole;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Roles.Commands;

public sealed class DeleteRoleEndpoint : IEndpoint
{
	public string GroupName => RolesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapDelete(pattern: "/{roleId:guid}", handler: HandleAsync).RequireRoot()
			.WithSummary(summary: "Delete a custom role")
			.WithDescription(description: "System roles (user/admin/root) cannot be deleted. Revokes the role's permissions from every current member first.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid roleId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		DeleteRoleCommand command = new DeleteRoleCommand(
			RoleId: roleId,
			DeletedBy: currentUser.UserId
		);

		Result<Unit, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
