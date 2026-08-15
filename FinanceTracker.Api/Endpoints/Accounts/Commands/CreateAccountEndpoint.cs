using FinanceTracker.Api.Endpoints.Accounts.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Accounts.Commands;

public sealed class CreateAccountEndpoint : IEndpoint
{
	public string GroupName => AccountsEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: String.Empty, handler: HandleAsync)
			.RequirePermission(resource: Resource.Account, action: PermissionAction.Write)
			.WithSummary(summary: "Create a new account")
			.WithDescription(description: "Creates an account for the authenticated user. Requires an Idempotency-Key header.")
			.Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict);
	}

	private static async Task<IHttpResult> HandleAsync(
		CreateAccountRequest request,
		LinkGenerator linkGenerator,
		ICurrentUserProvider currentUser,
		ISender sender,
		HttpContext httpContext,
		CancellationToken ct)
	{
		Guid idempotencyKey = httpContext.GetIdempotencyKey() ?? Guid.Empty;

		Result<Name, DomainException> name = Name.Create(value: request.Name);
		if (name.IsFailure)
			return name.Error!.ToValidationProblem(fieldName: nameof(request.Name));

		Result<Currency, DomainException> currency = Currency.Create(value: request.Currency);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem(fieldName: nameof(request.Currency));

		CreateAccountCommand command = new CreateAccountCommand(
			UserId: currentUser.UserId,
			Name: name.Value!,
			Type: request.Type,
			Currency: currency.Value!,
			InitialBalance: request.InitialBalance
		)
		{ IdempotencyKey = idempotencyKey };

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedAtRoute(
			linkGenerator: linkGenerator,
			httpContext: httpContext,
			routeName: RouteNames.GetAccount,
			routeValues: id => new { accountId = id }
		);
	}
}
