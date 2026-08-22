using FinanceTracker.Api.Endpoints.Categories.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Category.Queries.GetCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Categories.Queries;

public sealed class GetCategoryEndpoint : IEndpoint
{
	public string GroupName => CategoriesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{categoryId:guid}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Category, action: PermissionAction.Read)
			.WithName(endpointName: RouteNames.GetCategory)
			.WithSummary(summary: "Get a category by id")
			.Produces<CategoryResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid categoryId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetCategoryQuery query = new GetCategoryQuery(
			CategoryId: categoryId,
			UserId: currentUser.UserId
		);

		Result<CategoryReadModel, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<CategoryReadModel, CategoryResponse>();
	}
}
