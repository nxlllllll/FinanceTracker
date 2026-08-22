using FinanceTracker.Api.Endpoints.Budgets.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Budgets.Queries;

public sealed class GetBudgetEndpoint : IEndpoint
{
	public string GroupName => BudgetsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapGet(pattern: "/{budgetId:guid}", handler: HandleAsync)
			.RequirePermission(resource: Resource.Budget, action: PermissionAction.Read)
			.WithName(endpointName: RouteNames.GetBudget)
			.WithSummary(summary: "Get a budget by id")
			.Produces<BudgetResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid budgetId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		GetBudgetQuery query = new GetBudgetQuery(
			BudgetId: budgetId,
			UserId: currentUser.UserId
		);

		Result<BudgetReadModel, AppException> result = await sender.Send(request: query, cancellationToken: ct);

		return result.ToHttpResult<BudgetReadModel, BudgetResponse>();
	}
}
