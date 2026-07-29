using FinanceTracker.Api.Endpoints.Auth.Contracts;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Auth.Commands;

public sealed class RegisterEndpoint : IEndpoint
{
	public string GroupName => AuthEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/register", handler: HandleAsync).AllowAnonymous()
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
			return email.Error!.ToValidationProblem(fieldName: nameof(request.Email));

		Result<Currency, DomainException> currency = Currency.Create(value: request.BaseCurrency);
		if (currency.IsFailure)
			return currency.Error!.ToValidationProblem(fieldName: nameof(request.BaseCurrency));

		RegisterUserCommand command = new RegisterUserCommand(
			Email: email.Value!,
			Password: request.Password,
			BaseCurrencyCode: currency.Value!,
			IpAddress: httpContext.GetClientIpAddress()
		)
		{ IdempotencyKey = idempotencyKey.Value };

		Result<Guid, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToCreatedResult(locationFactory: id => $"/api/v1/users/{id}");
	}
}
