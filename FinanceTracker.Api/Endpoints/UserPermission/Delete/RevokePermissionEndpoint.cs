using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.UserPermission.Delete;

public sealed class RevokePermissionEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapDelete(pattern: "/users/{userId:guid}/permissions/{permission}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Permission, action: PermissionAction.Manage)
			.WithTags(tags: "Permissions")
			.WithSummary(summary: "Revoke a permission from a user")
			.WithDescription(description: "Requires permission:manage.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden);
	}

	private static async Task<IHttpResult> HandleAsync(
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
