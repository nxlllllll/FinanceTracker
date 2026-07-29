using FinanceTracker.Api.Endpoints.Roles.Contracts;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Queries.GetUserRoles;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Roles;

public sealed class GetUserRolesEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet(pattern: "/users/{userId:guid}/roles", handler: HandleAsync).RequireRoot()
			.WithTags(tags: "Roles")
			.WithSummary(summary: "List roles held by a user")
			.Produces<List<RoleResponse>>(statusCode: StatusCodes.Status200OK);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid userId,
		ISender sender,
		CancellationToken ct)
	{
		GetUserRolesQuery query = new GetUserRolesQuery(UserId: userId);

		Result<IReadOnlyList<RoleDto>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<RoleDto, RoleResponse>();
	}
}
