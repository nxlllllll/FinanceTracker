using FinanceTracker.Api.Http;
using FinanceTracker.Api.Routing;
using FinanceTracker.Application.UseCases.User.Commands.RevokeToken;
using MediatR;

namespace FinanceTracker.Api.Endpoints.Auth.Commands;

public sealed class LogoutEndpoint : IEndpoint
{
	public string GroupName => AuthEndpointGroup.GroupName;

	public void MapEndpoint(IEndpointRouteBuilder group)
	{
		group.MapPost(pattern: "/logout", handler: HandleAsync).AllowAnonymous()
			.WithSummary(summary: "Log out")
			.WithDescription(description: "Revokes the current session and clears the refresh token cookie. Idempotent.")
			.Produces(statusCode: StatusCodes.Status204NoContent); ;
	}

	internal static async Task<IHttpResult> HandleAsync(
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
