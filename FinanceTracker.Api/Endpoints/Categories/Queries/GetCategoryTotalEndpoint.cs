using FinanceTracker.Api.Endpoints.Categories.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Category.Queries.GetTotal;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Categories.Queries;

public sealed class GetCategoryTotalEndpoint : IEndpoint
{
	public string GroupName => CategoriesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{categoryId:guid}/totals/{period}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Category, action: PermissionAction.Read)
			.WithSummary(summary: "Get one category's total for a month")
			.WithDescription(description:
				"Period is any date within the month, as yyyy-MM-dd. Totals are in the user's base currency; while a base-currency" +
				"change is being applied the answer arrives with recalculationPending set and no total."
			).Produces<CategoryTotalResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid categoryId,
		DateOnly period,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetTotalQuery query = new GetTotalQuery(
			UserId: currentUser.UserId,
			CategoryId: categoryId,
			Period: period
		);

		Result<CategoryTotalView, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<CategoryTotalView, CategoryTotalResponse>();
	}
}
