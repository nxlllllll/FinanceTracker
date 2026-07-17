using FinanceTracker.Api.Contracts.Auth;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.User.Commands.RefreshToken;
using FinanceTracker.Application.UseCases.User.Commands.RevokeToken;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Infrastructure.Services.Token;
using MediatR;
using Microsoft.Extensions.Options;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Auth;

public sealed class SessionEndpoints : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/api/v1/auth/refresh", handler: RefreshAsync);
		app.MapPost(pattern: "/api/v1/auth/logout", handler: LogoutAsync);
	}

	private static async Task<IHttpResult> RefreshAsync(
		ISender sender,
		HttpContext httpContext,
		IOptions<JwtOptions> jwtOptions,
		CancellationToken ct)
	{
		string? refreshToken = RefreshTokenCookie.Read(httpContext: httpContext);
		if (refreshToken is null)
			return new InvalidTokenException(message: "Refresh token is missing.").ToProblem();

		RefreshTokenCommand command = new RefreshTokenCommand(
			RefreshToken: refreshToken,
			IpAddress: httpContext.GetClientIpAddress()
		);

		Result<SessionToken, AppException> result = await sender.Send(request: command, cancellationToken: ct);

		if (result.IsFailure)
		{
			RefreshTokenCookie.Delete(httpContext: httpContext);
			return result.Error!.ToProblem();
		}

		SessionToken session = result.Value!;

		RefreshTokenCookie.Append(httpContext: httpContext, refreshToken: session.RefreshToken, jwtOptions: jwtOptions);

		return Results.Ok(value: new SessionResponse(
			AccessToken: session.AccessToken,
			AccessTokenExpiresAt: session.AccessTokenExpiresAt
		));
	}

	private static async Task<IHttpResult> LogoutAsync(
		ISender sender,
		HttpContext httpContext,
		CancellationToken ct)
	{
		string? refreshToken = RefreshTokenCookie.Read(httpContext: httpContext);

		if (refreshToken is not null)
		{
			RevokeTokenCommand command = new RevokeTokenCommand(
				RefreshToken: refreshToken,
				IpAddress: httpContext.GetClientIpAddress()
			);

			await sender.Send(request: command, cancellationToken: ct);
		}

		RefreshTokenCookie.Delete(httpContext: httpContext);

		return Results.NoContent();
	}
}
