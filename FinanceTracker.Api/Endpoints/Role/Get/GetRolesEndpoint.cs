using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Contracts.Role.Response;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Role.Queries.GetRoles;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Role.Get;

public sealed class GetRolesEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet(pattern: "/api/v1/roles", handler: HandleAsync).RequireRoot()
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
