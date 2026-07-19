using System.Net.Mime;
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
		CancellationToken cancellationToken
	) => exception switch
	{
		BadHttpRequestException badRequest => await WriteProblemAsync(
			httpContext: httpContext,
			statusCode: badRequest.StatusCode,
			title: "Bad Request",
			detail: "The request body could not be read. Check field types and enum values.",
			logAction: () => logger.ZLogWarning(exception: badRequest, message: $"Malformed request for {httpContext.Request.Method} {httpContext.Request.Path}"),
			ct: cancellationToken
		),
		_ => await WriteProblemAsync(
			httpContext: httpContext,
			statusCode: StatusCodes.Status500InternalServerError,
			title: "Internal Server Error",
			detail: "An unexpected error occurred. Use the correlation id to investigate.",
			logAction: () => logger.ZLogError(exception: exception, message: $"Unhandled exception for {httpContext.Request.Method} {httpContext.Request.Path}"),
			ct: cancellationToken
		)
	};

	private static async ValueTask<bool> WriteProblemAsync(
		HttpContext httpContext,
		int statusCode,
		string title,
		string detail,
		Action logAction,
		CancellationToken ct)
	{
		logAction();

		httpContext.Response.StatusCode = statusCode;

		await httpContext.Response.WriteAsJsonAsync(
			value: new ProblemDetails
			{
				Status = statusCode,
				Title = title,
				Detail = detail
			},
			options: null,
			contentType: MediaTypeNames.Application.ProblemJson,
			cancellationToken: ct
		);
		
		return true;
	}
}
