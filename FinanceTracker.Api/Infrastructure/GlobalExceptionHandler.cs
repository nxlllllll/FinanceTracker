using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace FinanceTracker.Api.Infrastructure;

public sealed class GlobalExceptionHandler(
	ILogger<GlobalExceptionHandler> logger
) : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken)
	{
		logger.ZLogError(exception: exception, message: $"Unhandled exception for {httpContext.Request.Method} {httpContext.Request.Path}");

		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

		await httpContext.Response.WriteAsJsonAsync(value: new ProblemDetails
		{
			Status = StatusCodes.Status500InternalServerError,
			Title = "Internal Server Error",
			Detail = "An unexpected error occurred. Use the correlation id to investigate."
		}, cancellationToken: cancellationToken);

		return true;
	}
}
