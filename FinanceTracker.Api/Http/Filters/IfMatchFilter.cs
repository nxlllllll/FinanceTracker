using FinanceTracker.Api.Http.Results;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Api.Http.Filters;

/// <summary>
/// Rejects an <c>If-Match</c> header this API cannot evaluate, before the request reaches its handler.
/// </summary>
public sealed class IfMatchFilter : IEndpointFilter
{
	public async ValueTask<object?> InvokeAsync(
		EndpointFilterInvocationContext context,
		EndpointFilterDelegate next)
	{
		ParsedETag ifMatch = ETag.Parse(ifMatchHeaderValue: context.HttpContext.Request.Headers.IfMatch);

		if (!ifMatch.IsValid)
		{
			return new ValidationException(errors: new Dictionary<string, string[]>
			{
				["ifMatch"] = ["The If-Match header must be a single strong entity tag such as \"1\", or '*'."]
			}).ToProblem();
		}

		return await next(context: context);
	}
}
