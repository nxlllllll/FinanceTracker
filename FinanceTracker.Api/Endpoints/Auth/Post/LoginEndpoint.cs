using FinanceTracker.Api.Contracts.Auth.Request;
using FinanceTracker.Api.Contracts.Auth.Response;
using FinanceTracker.Api.Infrastructure;
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

namespace FinanceTracker.Api.Endpoints.Auth.Post;

public sealed class LoginEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
		=> app.MapPost(pattern: "/api/v1/auth/login", handler: HandleAsync);

	private static async Task<IHttpResult> HandleAsync(
		LoginUserRequest request,
		ISender sender,
		HttpContext httpContext,
		IOptions<JwtOptions> jwtOptions,
		CancellationToken ct)
	{
		Result<Email, DomainException> email = Email.Create(value: request.Email);
		if (email.IsFailure)
			return email.Error!.ToValidationProblem();

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
