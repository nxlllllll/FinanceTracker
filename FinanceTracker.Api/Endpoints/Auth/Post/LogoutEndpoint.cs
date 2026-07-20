using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Application.UseCases.User.Commands.RevokeToken;
using MediatR;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Endpoints.Auth.Post;

public sealed class LogoutEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost(pattern: "/api/v1/auth/logout", handler: HandleAsync)
			.WithTags(tags: "Auth")
			.WithSummary(summary: "Log out")
			.WithDescription(description: "Revokes the current session and clears the refresh token cookie. Idempotent.")
			.Produces(statusCode: StatusCodes.Status204NoContent);;
	}

	private static async Task<IHttpResult> HandleAsync(
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
