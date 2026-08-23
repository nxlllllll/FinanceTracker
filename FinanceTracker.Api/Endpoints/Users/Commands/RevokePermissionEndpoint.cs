using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class RevokePermissionEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapDelete(pattern: "/{userId:guid}/permissions/{permission}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Permission, action: PermissionAction.Manage)
			.WithSummary(summary: "Revoke a permission from a user")
			.WithDescription(description: "Requires permission:manage.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid userId,
		string permission,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Permission, DomainException> parsedPermission = Permission.Create(value: permission);
		if (parsedPermission.IsFailure)
			return parsedPermission.Error!.ToValidationProblem(fieldName: nameof(permission));

		RevokePermissionCommand command = new RevokePermissionCommand(
			TargetUserId: userId,
			Permission: parsedPermission.Value!,
			RevokedBy: currentUser.UserId
		);

		Result<Unit, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
