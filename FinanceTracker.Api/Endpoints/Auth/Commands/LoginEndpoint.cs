using FinanceTracker.Api.Endpoints.Auth.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Application.UseCases.User.Commands.LoginUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Services.Token;
using MediatR;
using Microsoft.Extensions.Options;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Auth;

public sealed class LoginEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/auth/login", handler: HandleAsync).AllowAnonymous()
			.WithTags(tags: "Auth")
			.WithSummary(summary: "Log in")
			.WithDescription(description: "Returns an access token in the body and sets a refresh token as an HttpOnly cookie.")
			.Produces<SessionResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status401Unauthorized);
	}

	private static async Task<IHttpResult> HandleAsync(
		LoginUserRequest request,
		ISender sender,
		HttpContext httpContext,
		IOptions<JwtOptions> jwtOptions,
		CancellationToken ct)
	{
		Result<Email, DomainException> email = Email.Create(value: request.Email);
		if (email.IsFailure)
			return email.Error!.ToValidationProblem(fieldName: nameof(request.Email));

		LoginUserCommand command = new LoginUserCommand(
			Email: email.Value!,
			Password: request.Password,
			IpAddress: httpContext.GetClientIpAddress()
		);

		Result<SessionToken, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		return result.ToHttpResult<SessionToken, SessionResponse>(onSuccess: session => RefreshTokenCookie.Append(
			httpContext: httpContext,
			refreshToken: session.RefreshToken,
			jwtOptions: jwtOptions
		));
	}
}
