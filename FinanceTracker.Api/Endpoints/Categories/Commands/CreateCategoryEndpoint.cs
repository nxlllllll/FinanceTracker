using FinanceTracker.Api.Endpoints.Categories.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Categories.Commands;

public sealed class CreateCategoryEndpoint : IEndpoint
{
	public string GroupName => CategoriesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.Category, action: PermissionAction.Write)
			.WithSummary(summary: "Create a category")
			.WithDescription(description:
				"Requires an Idempotency-Key header. Repeating a request with the same key returns the original result instead of creating a second category."
			).Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		CreateCategoryRequest request,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		LinkGenerator linkGenerator,
		ISender sender,
		CancellationToken ct)
	{
		Guid? idempotencyKey = httpContext.GetIdempotencyKey();
		if (idempotencyKey is null)
			return new EmptyIdempotencyKeyException(message: "A valid 'Idempotency-Key' header is required.").ToProblem();

		Result<Name, DomainException> name = Name.Create(value: request.Name);
		if (name.IsFailure)
			return name.Error!.ToValidationProblem(fieldName: nameof(request.Name));

		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: currentUser.UserId,
			Name: name.Value!,
			Type: request.Type,
			ParentId: request.ParentId
		) { IdempotencyKey = idempotencyKey.Value };

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedAtRoute(
			linkGenerator: linkGenerator,
			httpContext: httpContext,
			routeName: RouteNames.GetCategory,
			routeValues: categoryId => new { categoryId }
		);
	}
}
