using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Commands.RemoveRoleFromUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Roles;

public sealed class RemoveRoleFromUserEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapDelete(pattern: "/users/{userId:guid}/roles/{roleId:guid}", handler: HandleAsync).RequireRoot()
			.WithTags(tags: "Roles")
			.WithSummary(summary: "Remove a role from a user")
			.WithDescription(description: "Fails if this would remove the last remaining root user.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
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
