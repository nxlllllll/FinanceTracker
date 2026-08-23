using FinanceTracker.Api.Endpoints.Users.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Commands;

public sealed class GrantPermissionEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{userId:guid}/permissions", handler: HandleAsync)
			.RequirePermission(resource: Resource.Permission, action: PermissionAction.Manage)
			.WithSummary(summary: "Grant a permission to a user")
			.WithDescription(description: "Requires permission:manage. Format: \"resource:action\", e.g. \"account:write\".")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid userId,
		GrantPermissionRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		Result<Permission, DomainException> permission = Permission.Create(value: request.Permission);
		if (permission.IsFailure)
			return permission.Error!.ToValidationProblem(fieldName: nameof(request.Permission));

		GrantPermissionCommand command = new GrantPermissionCommand(
			TargetUserId: userId,
			Permission: permission.Value!,
			GrantedBy: currentUser.UserId
		);

		Result<Unit, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
