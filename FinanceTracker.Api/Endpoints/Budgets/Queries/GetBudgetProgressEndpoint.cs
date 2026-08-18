using FinanceTracker.Api.Endpoints.Budgets.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Budget.Queries.GetBudgetProgress;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Budgets.Queries;

public sealed class GetBudgetProgressEndpoint : IEndpoint
{
	public string GroupName => BudgetsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{budgetId:guid}/progress", handler: HandleAsync)
			.RequirePermission(resource: Resource.Budget, action: PermissionAction.Read)
			.WithSummary(summary: "Get how much of a budget has been spent")
			.WithDescription(description:
				"Counts only debits in the budget's category that fall inside its period and are not excluded " +
				"from analytics. Figures are in the budget's own currency. Remaining goes negative and percentage " +
				"passes 100 once the budget is overspent — neither is clamped."
			).Produces<BudgetProgressResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid budgetId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetBudgetProgressQuery query = new GetBudgetProgressQuery(
			BudgetId: budgetId,
			UserId: currentUser.UserId
		);

		Result<BudgetProgress, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<BudgetProgress, BudgetProgressResponse>();
	}
}
