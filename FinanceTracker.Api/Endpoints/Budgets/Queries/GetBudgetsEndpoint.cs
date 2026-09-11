using FinanceTracker.Api.Endpoints.Budgets.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Budget.Queries.GetBudgets;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Budgets.Queries;

public sealed class GetBudgetsEndpoint : IEndpoint
{
	public string GroupName => BudgetsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.Budget, action: PermissionAction.Read)
			.WithSummary(summary: "List budgets")
			.WithDescription(description:
				"Newest first, including deactivated ones — pass isActive to narrow to one or the other, and " +
				"categoryId to a single category. Pages forward with a cursor: send back nextCursorDate " +
				"and nextCursorId from the previous page — both or neither. Page size is 1 to 100."
			).Produces<PagedResponse<BudgetResponse>>(statusCode: StatusCodes.Status200OK)
			.ProducesValidationProblem();
	}

	internal static async Task<IHttpResult> HandleAsync(
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct,
		Guid? categoryId = null,
		bool? isActive = null,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20)
	{
		GetBudgetsQuery query = new GetBudgetsQuery(
			UserId: currentUser.UserId,
			CategoryId: categoryId,
			IsActive: isActive,
			CursorCreatedAt: cursorCreatedAt?.ToUniversalTime(),
			CursorId: cursorId,
			PageSize: pageSize
		);

		Result<PagedResult<BudgetReadModel>, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToPagedHttpResult<BudgetReadModel, BudgetResponse>();
	}
}
