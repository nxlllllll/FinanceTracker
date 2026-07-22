using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Contracts.UserPermission.Request;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.UserPermission.Post;

public sealed class GrantPermissionEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/api/v1/users/{userId:guid}/permissions", handler: HandleAsync)
			.RequirePermission(resource: Resource.Permission, action: PermissionAction.Manage)
			.WithTags(tags: "Permissions")
			.WithSummary(summary: "Grant a permission to a user")
			.WithDescription(description: "Requires permission:manage. Format: \"resource:action\", e.g. \"account:write\".")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid userId,
		GrantPermissionRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Permission, DomainException> permission = Permission.Create(value: request.Permission);
		if (permission.IsFailure)
			return permission.Error!.ToValidationProblem();

		GrantPermissionCommand command = new GrantPermissionCommand(
			TargetUserId: userId,
			Permission: permission.Value!,
			GrantedBy: currentUser.UserId
		);

		Result<Unit, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
