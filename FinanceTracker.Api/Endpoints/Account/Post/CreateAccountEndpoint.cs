using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Contracts.Abstractions;
using FinanceTracker.Api.Contracts.Account.Request;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Account.Post;

public sealed class CreateAccountEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/api/v1/accounts", handler: HandleAsync)
			.RequireAuthorization()
			.RequirePermission(resource: Resource.Account, action: PermissionAction.Write)
			.WithTags(tags: "Accounts")
			.WithSummary(summary: "Create an account")
			.WithDescription(description: "Requires account:write permission and an Idempotency-Key header.")
			.Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden)
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict);
	}

	private static async Task<IHttpResult> HandleAsync(
		CreateAccountRequest request,
		ICurrentUserProvider currentUser,
		ISender sender,
		HttpContext httpContext,
		CancellationToken ct)
	{
		Guid? idempotencyKey = httpContext.GetIdempotencyKey();
		if (idempotencyKey is null)
			return new EmptyIdempotencyKeyException(message: "A valid 'Idempotency-Key' header is required.").ToProblem();

		Result<Name, DomainException> name = Name.Create(value: request.Name);
		if (name.IsFailure)
			return name.Error!.ToValidationProblem();

		Result<Currency, DomainException> currency = Currency.Create(value: request.Currency);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem();

		CreateAccountCommand command = new CreateAccountCommand(
			UserId: currentUser.UserId,
			Name: name.Value!,
			Type: request.Type,
			Currency: currency.Value!,
			InitialBalance: request.InitialBalance
		)
		{ IdempotencyKey = idempotencyKey.Value };

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedResult(locationFactory: id => $"/api/v1/accounts/{id}");
	}
}
