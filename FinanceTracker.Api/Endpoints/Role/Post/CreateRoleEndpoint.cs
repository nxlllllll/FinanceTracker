using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Contracts.Abstractions;
using FinanceTracker.Api.Contracts.Role.Request;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Role.Commands.CreateRole;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Role.Post;

public sealed class CreateRoleEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/api/v1/roles", handler: HandleAsync).RequireRoot()
			.WithTags(tags: "Roles")
			.WithSummary(summary: "Create a custom role")
			.Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden);
	}

	private static async Task<IHttpResult> HandleAsync(
		CreateRoleRequest request,
		ISender sender,
		CancellationToken ct)
	{
		Result<Name, DomainException> displayName = Name.Create(value: request.DisplayName);
		if (displayName.IsFailure)
			return displayName.Error!.ToValidationProblem();

		Result<IReadOnlySet<Permission>, ValidationException> permissions = PermissionSetParser.Parse(raw: request.Permissions);
		if (permissions.IsFailure)
			return permissions.Error!.ToProblem();

		CreateRoleCommand command = new CreateRoleCommand(
			DisplayName: displayName.Value,
			Permissions: permissions.Value!
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedResult(locationFactory: id => $"/api/v1/roles/{id}");
	}
}
