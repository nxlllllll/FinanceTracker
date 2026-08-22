using FinanceTracker.Api.Endpoints.Budgets.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Budgets.Commands;

public sealed class ChangeBudgetAmountEndpoint : IEndpoint
{
	public string GroupName => BudgetsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPatch(pattern: "/{budgetId:guid}/amount", handler: HandleAsync)
			.RequirePermission(resource: Resource.Budget, action: PermissionAction.Write)
			.WithSummary(summary: "Change how much a budget allows")
			.WithDescription(description:
				"The currency stays as it was — only the figure moves. Already-recorded spending is not " +
				"re-evaluated, so a budget lowered below what is already spent simply reads as overspent."
			).Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid budgetId,
		ChangeBudgetAmountRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ChangeBudgetAmountCommand command = new ChangeBudgetAmountCommand(
			UserId: currentUser.UserId,
			BudgetId: budgetId,
			Amount: request.Amount
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
