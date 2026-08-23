using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Budgets.Commands;

public sealed class ActivateBudgetEndpoint : IEndpoint
{
	public string GroupName => BudgetsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/{budgetId:guid}/activate", handler: HandleAsync)
			.RequirePermission(resource: Resource.Budget, action: PermissionAction.Write)
			.WithSummary(summary: "Resume tracking against a budget")
			.WithDescription(description: "Activating one that was never deactivated succeeds and changes nothing.")
			.Produces(statusCode: StatusCodes.Status204NoContent)
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	internal static async Task<IHttpResult> HandleAsync(
		Guid budgetId,
		ICurrentUserProvider currentUser,
		ISender sender,
		CancellationToken ct)
	{
		ActivateBudgetCommand command = new ActivateBudgetCommand(
			UserId: currentUser.UserId,
			BudgetId: budgetId
		);

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToNoContentResult();
	}
}
