using FinanceTracker.Api.Endpoints.Roles.Contracts;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Queries.GetRoles;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Roles;

public sealed class GetRolesEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet(pattern: "/roles", handler: HandleAsync).RequireRoot()
			.WithTags(tags: "Roles")
			.WithSummary(summary: "List all roles")
			.Produces<List<RoleResponse>>(statusCode: StatusCodes.Status200OK);
	}

	private static async Task<IHttpResult> HandleAsync(
		ISender sender,
		CancellationToken ct)
	{
		Result<IReadOnlyList<RoleDto>, AppException> result = await sender.Send(request: new GetRolesQuery(), cancellationToken: ct);

		return result.ToHttpResult<RoleDto, RoleResponse>();
	}
}
