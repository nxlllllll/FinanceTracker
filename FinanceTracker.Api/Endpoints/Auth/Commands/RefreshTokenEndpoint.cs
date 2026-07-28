using FinanceTracker.Api.Endpoints.Auth.Contracts;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Api.Routing;
using FinanceTracker.Application.UseCases.User.Commands.RefreshToken;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Infrastructure.Services.Token;
using MediatR;
using Microsoft.Extensions.Options;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Auth.Commands;

public sealed class RefreshTokenEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/auth/refresh", handler: HandleAsync).AllowAnonymous()
			.WithTags(tags: "Auth")
			.WithSummary(summary: "Refresh the access token")
			.WithDescription(description: "Rotates the refresh token cookie and issues a new access token.")
			.Produces<SessionResponse>(statusCode: StatusCodes.Status200OK)
			.ProducesProblem(statusCode: StatusCodes.Status401Unauthorized);
	}

	private static async Task<IHttpResult> HandleAsync(
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

		return result.ToHttpResult<SessionToken, SessionResponse>(
			onSuccess: session => RefreshTokenCookie.Append(
				httpContext: httpContext,
				refreshToken: session.RefreshToken,
				jwtOptions: jwtOptions
			),
			onError: _ => RefreshTokenCookie.Delete(httpContext: httpContext)
		);
	}
}
