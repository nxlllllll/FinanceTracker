using FinanceTracker.Api.Endpoints.Categories.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Category.Queries.GetCategories;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Categories.Queries;

public sealed class GetCategoriesEndpoint : IEndpoint
{
	public string GroupName => CategoriesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.Category, action: PermissionAction.Read)
			.WithSummary(summary: "List categories")
			.WithDescription(description:
				"Pages forward with a cursor: send back nextCursorDate and nextCursorId from the previous page — both or neither." +
				"Page size is 1 to 100. The type filter takes the same values responses carry, in any casing." +
				"Filters narrow the set the cursor walks, so changing one mid-walk starts a different sequence and the cursor should be dropped."
			).Produces<PagedResponse<CategoryResponse>>(statusCode: StatusCodes.Status200OK)
			.ProducesValidationProblem();
	}

	private static async Task<IHttpResult> HandleAsync(
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct,
		string? type = null,
		bool? isArchived = null,
		Guid? parentId = null,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20)
	{
		Result<CategoryType?, ValidationException> parsedType = EnumQuery.ParseOptional<CategoryType>(
			value: type,
			parameterName: nameof(type)
		);

		if (parsedType.IsFailure)
			return parsedType.Error!.ToProblem();

		GetCategoriesQuery query = new GetCategoriesQuery(
			UserId: currentUser.UserId,
			Type: parsedType.Value,
			IsArchived: isArchived,
			ParentId: parentId,
			CursorCreatedAt: cursorCreatedAt?.ToUniversalTime(),
			CursorId: cursorId,
			PageSize: pageSize
		);

		Result<PagedResult<CategoryReadModel>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToPagedHttpResult<CategoryReadModel, CategoryResponse>();
	}
}
