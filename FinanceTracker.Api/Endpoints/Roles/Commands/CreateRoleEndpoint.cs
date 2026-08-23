using FinanceTracker.Api.Endpoints.Roles.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Role.Commands.CreateRole;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Roles.Commands;

public sealed class CreateRoleEndpoint : IEndpoint
{
	public string GroupName => RolesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: String.Empty, handler: HandleAsync).RequireRoot()
			.WithSummary(summary: "Create a custom role")
			.WithDescription(description: "Requires the root role and an Idempotency-Key header.")
			.Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict);
	}

	internal static async Task<IHttpResult> HandleAsync(
		CreateRoleRequest request,
		LinkGenerator linkGenerator,
		ISender sender,
		HttpContext httpContext,
		CancellationToken ct)
	{
		Guid idempotencyKey = httpContext.GetIdempotencyKey() ?? Guid.Empty;

		Result<Name, DomainException> displayName = Name.Create(value: request.DisplayName);
		if (displayName.IsFailure)
			return displayName.Error!.ToValidationProblem(fieldName: nameof(request.DisplayName));

		Result<IReadOnlySet<Permission>, ValidationException> permissions = PermissionSetParser.Parse(raw: request.Permissions);
		if (permissions.IsFailure)
			return permissions.Error!.ToProblem();

		CreateRoleCommand command = new CreateRoleCommand(
			DisplayName: displayName.Value,
			Permissions: permissions.Value!
		)
		{ IdempotencyKey = idempotencyKey };

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedAtRoute(
			linkGenerator: linkGenerator,
			httpContext: httpContext,
			routeName: RouteNames.GetRole,
			routeValues: id => new { roleId = id }
		);
	}
}
