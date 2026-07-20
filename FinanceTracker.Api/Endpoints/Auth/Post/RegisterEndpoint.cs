using FinanceTracker.Api.Contracts.Abstractions;
using FinanceTracker.Api.Contracts.Auth.Request;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Auth.Post;

public sealed class RegisterEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/api/v1/auth/register", handler: HandleAsync)
			.WithTags(tags: "Auth")
			.WithSummary(summary: "Register a new user")
			.WithDescription(description: "Creates a user account. Requires an Idempotency-Key header.")
			.Produces<CreatedIdResponse>(statusCode: StatusCodes.Status201Created)
			.ProducesValidationProblem()
			.ProducesProblem(statusCode: StatusCodes.Status409Conflict);
	}

	private static async Task<IHttpResult> HandleAsync(
		RegisterUserRequest request,
		ISender sender,
		HttpContext httpContext,
		CancellationToken ct)
	{
		Guid? idempotencyKey = httpContext.GetIdempotencyKey();
		if (idempotencyKey is null)
			return new EmptyIdempotencyKeyException(message: "A valid 'Idempotency-Key' header is required.").ToProblem();

		Result<Email, DomainException> email = Email.Create(value: request.Email);
		if (email.IsFailure)
			return email.Error!.ToValidationProblem();

		Result<Currency, DomainException> currency = Currency.Create(value: request.BaseCurrency);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem();

		RegisterUserCommand command = new RegisterUserCommand(
			Email: email.Value!,
			Password: request.Password,
			BaseCurrencyCode: currency.Value!,
			IpAddress: httpContext.GetClientIpAddress()
		) { IdempotencyKey = idempotencyKey.Value };

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedResult(locationFactory: id => $"/api/v1/users/{id}");
	}
}
