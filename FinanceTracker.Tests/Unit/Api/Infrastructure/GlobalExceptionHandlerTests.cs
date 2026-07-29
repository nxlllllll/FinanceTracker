using System.Text.Json;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Core.Services.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class GlobalExceptionHandlerTests
{
	private ICorrelationContext _correlationContext = null!;
	private GlobalExceptionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_correlationContext = Substitute.For<ICorrelationContext>();
		_handler = new GlobalExceptionHandler(
			logger: Substitute.For<ILogger<GlobalExceptionHandler>>()
		);
	}

	private DefaultHttpContext BuildContext()
	{
		IServiceProvider services = Substitute.For<IServiceProvider>();
		services.GetService(serviceType: typeof(ICorrelationContext)).Returns(returnThis: _correlationContext);

		return new DefaultHttpContext
		{
			RequestServices = services,
			Response = { Body = new MemoryStream() }
		};
	}

	private static async Task<ProblemDetails> ReadProblemAsync(DefaultHttpContext context)
	{
		context.Response.Body.Seek(offset: 0, origin: SeekOrigin.Begin);
		return (await JsonSerializer.DeserializeAsync<ProblemDetails>(utf8Json: context.Response.Body))!;
	}

	[Test]
	public async Task TryHandleAsync_WhenCorrelationIdIsSet_ShouldIncludeItAsTraceId()
	{
		Guid correlationId = Guid.CreateVersion7();
		_correlationContext.CorrelationId.Returns(returnThis: correlationId);

		DefaultHttpContext context = BuildContext();

		await _handler.TryHandleAsync(
			httpContext: context,
			exception: new InvalidOperationException(message: "boom"),
			cancellationToken: CancellationToken.None
		);

		ProblemDetails problem = await ReadProblemAsync(context: context);

		await Assert.That(value: problem.Extensions["traceId"]!.ToString()).IsEqualTo(expected: correlationId.ToString());
	}

	[Test]
	public async Task TryHandleAsync_WhenCorrelationIdIsEmpty_ShouldFallBackToHttpContextTraceIdentifier()
	{
		_correlationContext.CorrelationId.Returns(returnThis: Guid.Empty);

		DefaultHttpContext context = BuildContext();
		context.TraceIdentifier = "fallback-trace-id";

		await _handler.TryHandleAsync(
			httpContext: context,
			exception: new InvalidOperationException(message: "boom"),
			cancellationToken: CancellationToken.None
		);

		ProblemDetails problem = await ReadProblemAsync(context: context);

		await Assert.That(value: problem.Extensions["traceId"]!.ToString()).IsEqualTo(expected: "fallback-trace-id");
	}

	[Test]
	public async Task TryHandleAsync_ForGenericException_ShouldReturn500()
	{
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		DefaultHttpContext context = BuildContext();

		await _handler.TryHandleAsync(
			httpContext: context,
			exception: new InvalidOperationException(message: "boom"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status500InternalServerError);
	}

	[Test]
	public async Task TryHandleAsync_ForBadHttpRequestException_ShouldReturnItsOwnStatusCode()
	{
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		DefaultHttpContext context = BuildContext();

		BadHttpRequestException badRequest = new BadHttpRequestException(
			message: "malformed",
			statusCode: StatusCodes.Status400BadRequest
		);

		await _handler.TryHandleAsync(
			httpContext: context,
			exception: badRequest,
			cancellationToken: CancellationToken.None
		);


		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status400BadRequest);
	}

	[Test]
	public async Task TryHandleAsync_ShouldAlwaysReturnTrue()
	{
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		DefaultHttpContext context = BuildContext();

		bool handled = await _handler.TryHandleAsync(
			httpContext: context,
			exception: new Exception(message: "boom"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: handled).IsTrue();
	}
}
