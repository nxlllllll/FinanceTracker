using FinanceTracker.Api.Infrastructure;

namespace FinanceTracker.Api.Endpoints;

public sealed class PingEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet(pattern: "/ping", handler: () => Results.Ok(value: "pong"));

		app.MapGet(
			pattern: "/ping/me",
			handler: (ICurrentUserProvider currentUser) => Results.Ok(value: new { userId = currentUser.UserId, sessionId = currentUser.SessionId })
		).RequireAuthorization();
	}
}
