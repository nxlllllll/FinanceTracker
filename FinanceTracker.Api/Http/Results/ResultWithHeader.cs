namespace FinanceTracker.Api.Http.Results;

public sealed class ResultWithHeader(
	IResult inner,
	string headerName,
	string headerValue
) : IResult
{
	public async Task ExecuteAsync(HttpContext httpContext)
	{
		httpContext.Response.Headers[headerName] = headerValue;
		await inner.ExecuteAsync(httpContext: httpContext);
	}
}
