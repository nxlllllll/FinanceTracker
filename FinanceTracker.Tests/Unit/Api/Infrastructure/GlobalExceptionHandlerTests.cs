using System.Text.Json;
using FinanceTracker.Api.Http.Results;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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

	private DefaultHttpContext BuildContextWithRealServices()
	{
		ServiceCollection collection = new ServiceCollection();
		collection.AddLogging();
		collection.AddProblemDetails();
		collection.AddSingleton(implementationInstance: _correlationContext);

		return new DefaultHttpContext
		{
			RequestServices = collection.BuildServiceProvider(),
			Response = { Body = new MemoryStream() }
		};
	}

	[Test]
	public async Task TryHandleAsync_ForAThrownTransientException_ShouldAnswerWithItsMappedStatusAndRetryAfter()
	{
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		DefaultHttpContext context = BuildContextWithRealServices();

		CurrencyRateMissingException missingRate = new CurrencyRateMissingException(
			message: "No rate for USD to EUR.",
			fromCurrency: Currency.Reconstitute(value: "USD"),
			toCurrency: Currency.Reconstitute(value: "EUR")
		);

		await _handler.TryHandleAsync(
			httpContext: context,
			exception: missingRate,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status503ServiceUnavailable).Because(message: """
			A missing rate is filled in by the rate job on its own, so the caller should be told to come
			back rather than told the server failed. CurrencyConversionService signals this by throwing,
			not by returning a Result, so the mapping in ToProblem is only reachable if the exception
			handler applies it — otherwise transfers, transaction creation, base-currency changes and the
			total balance query all answer 500. The architecture test covering this checks the type
			hierarchy, which stays correct either way.
		""");

		await Assert.That(value: context.Response.Headers.RetryAfter.ToString()).IsEqualTo(expected: "60").Because(message: """
			Without Retry-After the client has nothing to schedule a retry against, which is the entire
			difference between this and a 500.
		""");
	}

	[Test]
	public async Task TryHandleAsync_ForAConcurrencyConflictThatOutlivedItsRetries_ShouldAnswerWith409()
	{
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		DefaultHttpContext context = BuildContextWithRealServices();

		await _handler.TryHandleAsync(
			httpContext: context,
			exception: new ConcurrencyConflictException(message: "Conflict.", id: Guid.CreateVersion7()),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status409Conflict).Because(message: """
			ConcurrencyRetryBehaviour rethrows once MaxRetries is spent, and TransientRetryBehaviour does
			not catch it — its filter is the Npgsql fault detector. So under sustained contention this
			arrives here as an exception, and answering 500 would tell the caller the server broke when
			retrying the request is exactly what would work.
		""");
	}

	[Test]
	public async Task TryHandleAsync_ForAThrownValidationException_ShouldAnswerWith400AndKeepTheFieldErrors()
	{
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		DefaultHttpContext context = BuildContextWithRealServices();

		ValidationException validation = new ValidationException(errors: new Dictionary<string, string[]>
		{
			["amount"] = ["Amount must be greater than zero."]
		});

		await _handler.TryHandleAsync(
			httpContext: context,
			exception: validation,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: context.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status400BadRequest);

		ProblemDetails problem = await ReadProblemAsync(context: context);

		await Assert.That(value: problem.Extensions.ContainsKey(key: "errors")).IsTrue().Because(message: """
			A validation failure that reaches here still names the fields at fault. Collapsing it into a
			generic problem would leave the caller with a 400 and nothing to correct.
		""");
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
