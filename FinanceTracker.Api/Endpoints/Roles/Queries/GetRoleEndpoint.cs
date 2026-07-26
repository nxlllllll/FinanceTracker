using FinanceTracker.Api.Endpoints.Roles.Contracts;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Queries.GetRole;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Roles;

public sealed class GetRoleEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet(pattern: "/roles/{roleId:guid}", handler: HandleAsync).RequireRoot()
			.WithTags(tags: "Roles")
			.WithSummary(summary: "Get a role by id")
			.Produces<RoleResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid roleId,
		ISender sender,
		CancellationToken ct)
	{
		GetRoleQuery query = new GetRoleQuery(RoleId: roleId);

		Result<RoleDto, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<RoleDto, RoleResponse>();
	}
}
