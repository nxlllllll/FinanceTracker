using FinanceTracker.Api.Endpoints.Budgets.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Budgets.Commands;

public sealed class CreateBudgetEndpoint : IEndpoint
{
	public string GroupName => BudgetsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/", handler: HandleAsync)
			.RequirePermission(resource: Resource.Budget, action: PermissionAction.Write)
			.WithSummary(summary: "Set a budget for a category over a period")
			.WithDescription(description:
				"Requires an Idempotency-Key header. Only one budget may cover a category on any given day: " +
				"an overlapping period is refused with budget.overlapping_period. The refusal is backed by an " +
				"exclusion constraint, so two requests racing to claim the same days cannot both succeed."
			).Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status404NotFound)
			.ProducesProblem(statusCode: StatusCodes.Status422UnprocessableEntity);
	}

	private static async Task<IHttpResult> HandleAsync(
		CreateBudgetRequest request,
		HttpContext httpContext,
		ICurrentUserProvider currentUser,
		LinkGenerator linkGenerator,
		ISender sender,
		CancellationToken ct)
	{
		Guid? idempotencyKey = httpContext.GetIdempotencyKey();
		if (idempotencyKey is null)
			return new EmptyIdempotencyKeyException(message: "A valid 'Idempotency-Key' header is required.").ToProblem();

		Result<Currency, DomainException> currency = Currency.Create(value: request.Currency);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem(fieldName: nameof(request.Currency));

		CreateBudgetCommand command = new CreateBudgetCommand(
			UserId: currentUser.UserId,
			CategoryId: request.CategoryId,
			Currency: currency.Value,
			Amount: request.Amount,
			From: request.From,
			To: request.To
		)
		{
			IdempotencyKey = idempotencyKey.Value
		};

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedAtRoute(
			linkGenerator: linkGenerator,
			httpContext: httpContext,
			routeName: RouteNames.GetBudget,
			routeValues: budgetId => new { budgetId }
		);
	}
}
