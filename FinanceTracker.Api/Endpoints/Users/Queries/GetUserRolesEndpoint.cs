using FinanceTracker.Api.Endpoints.Roles.Contracts;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Queries.GetUserRoles;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Users.Queries;

public sealed class GetUserRolesEndpoint : IEndpoint
{
	public string GroupName => UsersEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{userId:guid}/roles", handler: HandleAsync).RequireRoot()
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
