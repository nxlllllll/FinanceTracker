using FinanceTracker.Api.Endpoints.Roles.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Commands.UpdateRolePermissions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Roles.Commands;

public sealed class UpdateRolePermissionsEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPatch(pattern: "/roles/{roleId:guid}/permissions", handler: HandleAsync).RequireRoot()
			.WithTags(tags: "Roles")
			.WithSummary(summary: "Replace a role's permission set")
			.WithDescription(description: "Fans out Grant/Revoke to every current member of the role, so membership stays live.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid roleId,
		UpdateRolePermissionsRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<IReadOnlySet<Permission>, ValidationException> permissions = PermissionSetParser.Parse(raw: request.Permissions);
		if (permissions.IsFailure)
			return permissions.Error!.ToProblem();

		UpdateRolePermissionsCommand command = new UpdateRolePermissionsCommand(
			RoleId: roleId,
			NewPermissions: permissions.Value!,
			UpdatedBy: currentUser.UserId
		);

		Result<Unit, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
