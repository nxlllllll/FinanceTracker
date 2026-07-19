using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace FinanceTracker.Api.Auth;

/// <summary>
/// Wraps the default authorization result handler so a 403 (failed <see cref="PermissionRequirement"/>,
/// or any other forbidden outcome) comes back as ProblemDetails — consistent with every other error
/// in the API. 401 (missing/invalid token) is left untouched: it's a framework-level "you're not
/// authenticated" response, not something the pipeline maps via <c>ResultExtensions</c>.
/// </summary>
public sealed class ForbiddenProblemDetailsAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
	private readonly AuthorizationMiddlewareResultHandler _default = new AuthorizationMiddlewareResultHandler();

	public async Task HandleAsync(
		RequestDelegate next,
		HttpContext context,
		AuthorizationPolicy policy,
		PolicyAuthorizationResult authorizeResult)
	{
		if (!authorizeResult.Forbidden)
		{
			await _default.HandleAsync(next: next, context: context, policy: policy, authorizeResult: authorizeResult);
			return;
		}

		context.Response.StatusCode = StatusCodes.Status403Forbidden;

		IProblemDetailsService? problemDetailsService = context.RequestServices.GetService<IProblemDetailsService>();
		if (problemDetailsService is null)
		{
			await _default.HandleAsync(next: next, context: context, policy: policy, authorizeResult: authorizeResult);
			return;
		}

		await problemDetailsService.WriteAsync(context: new ProblemDetailsContext
		{
			HttpContext = context,
			ProblemDetails =
			{
				Status = StatusCodes.Status403Forbidden,
				Title = "Forbidden",
				Detail = "You do not have permission to perform this action."
			}
		});
	}
}
