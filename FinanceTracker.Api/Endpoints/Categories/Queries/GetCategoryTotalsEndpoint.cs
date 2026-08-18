using FinanceTracker.Api.Endpoints.Categories.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Categories.Queries;

public sealed class GetCategoryTotalsEndpoint : IEndpoint
{
	public string GroupName => CategoriesEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/totals/{period}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Category, action: PermissionAction.Read)
			.WithSummary(summary: "Get every category's total for a month")
			.WithDescription(description: "Period is any date within the month, as yyyy-MM-dd. Categories with no spending that month are absent rather than listed at zero.")
			.Produces<CategoryTotalsResponse>(statusCode: StatusCodes.Status200OK);
	}

	private static async Task<IHttpResult> HandleAsync(
		DateOnly period,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetTotalsByPeriodQuery query = new GetTotalsByPeriodQuery(
			UserId: currentUser.UserId,
			Period: period
		);

		Result<CategoryTotalsView, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<CategoryTotalsView, CategoryTotalsResponse>();
	}
}
