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

namespace FinanceTracker.Api.Endpoints.Auth.Commands;

public sealed class LoginEndpoint : IEndpoint
{
	public string GroupName => AuthEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/login", handler: HandleAsync).AllowAnonymous()
			.WithSummary(summary: "Log in")
			.WithDescription(description: "Returns an access token in the body and sets a refresh token as an HttpOnly cookie.")
			.Produces<SessionResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status401Unauthorized);
	}

	internal static async Task<IHttpResult> HandleAsync(
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
