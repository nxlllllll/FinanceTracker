using FinanceTracker.Api.Endpoints.Budgets.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Budgets.Commands;

public sealed class ChangeBudgetPeriodEndpoint : IEndpoint
{
	public string GroupName => BudgetsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{budgetId:guid}/period", handler: HandleAsync)
			.RequirePermission(resource: Resource.Budget, action: PermissionAction.Write)
			.WithSummary(summary: "Move a budget to a different period")
			.WithDescription(description:
				"Both ends are inclusive dates. Refused with budget.overlapping_period if the new span " +
				"would collide with another budget for the same category. Progress is recalculated " +
				"against the new span, so spending that fell outside it stops counting."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		Guid budgetId,
		ChangeBudgetPeriodRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeBudgetPeriodCommand command = new ChangeBudgetPeriodCommand(
			UserId: currentUser.UserId,
			BudgetId: budgetId,
			From: request.From,
			To: request.To
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
