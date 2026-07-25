using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Role.Commands.AssignRoleToUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Role.Post;

public sealed class AssignRoleToUserEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/users/{userId:guid}/roles/{roleId:guid}", handler: HandleAsync).RequireRoot()
			.WithTags(tags: "Roles")
			.WithSummary(summary: "Assign a role to a user")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
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
