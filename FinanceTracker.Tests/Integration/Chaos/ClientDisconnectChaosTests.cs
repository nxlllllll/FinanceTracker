using FinanceTracker.Api.Http.Results;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Infrastructure.Services.Correlation;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Tests.Integration.Chaos;

public sealed class ClientDisconnectChaosTests
{
	private sealed class Observations
	{
		public volatile bool AbortReachedEndpoint;
		public volatile bool HandlerConsulted;
		public volatile string? ConsultedWith;
		public volatile string? Escaped;
	}

	private sealed class ProbeExceptionHandler(Observations observations) : IExceptionHandler
	{
		public ValueTask<bool> TryHandleAsync(
			HttpContext httpContext,
			Exception exception,
			CancellationToken cancellationToken
		)
		{
			observations.HandlerConsulted = true;
			observations.ConsultedWith = exception.GetType().Name;

			return ValueTask.FromResult(result: false);
		}
	}

	private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(value: 15);
	private static readonly TimeSpan SettleWindow = TimeSpan.FromSeconds(value: 3);

	[Test]
	public async Task ClientHangingUp_ShouldNotReachAnyExceptionHandler()
	{
		Observations observations = await RunDisconnectAsync(failAfterAbort: false);

		await Assert.That(value: observations.AbortReachedEndpoint).IsTrue().Because(message: """
			Without this the rest of the test is vacuous: it would be measuring a request that was never
			actually abandoned.
		""");

		await Assert.That(value: observations.HandlerConsulted).IsFalse().Because(message: $"""
			ExceptionHandlerMiddleware absorbs a cancellation raised on an aborted request without
			consulting any handler. Observed: escaped={observations.Escaped ?? "none"},
			consultedWith={observations.ConsultedWith ?? "-"}.

			If this ever starts passing through, GlobalExceptionHandler will begin seeing client
			disconnects as ordinary failures — logging each one at error level and attempting a write to
			a dead socket — and will need a branch to classify them, which is currently absent because
			it would be unreachable.
		""");

		await Assert.That(value: observations.Escaped).IsNull().Because(message: """
			The middleware absorbs the exception rather than rethrowing it, so nothing surfaces upstream
			either. This is what makes the disconnect invisible to the application, which is the correct
			outcome and the reason no handling is needed.
		""");
	}

	[Test]
	public async Task FailureAfterClientHangsUp_ShouldStillReachTheExceptionHandler()
	{
		Observations observations = await RunDisconnectAsync(failAfterAbort: true);

		await Assert.That(value: observations.AbortReachedEndpoint).IsTrue().Because(message: """
			The failure has to happen after the caller is already gone. If the abort never arrived, the
			endpoint threw into a live connection and this measures nothing about the case in question.
		""");

		await Assert.That(value: observations.HandlerConsulted).IsTrue().Because(message: $"""
			A genuine failure is not a cancellation, so the middleware's absorption of aborted requests
			does not cover it. Observed: escaped={observations.Escaped ?? "none"},
			consultedWith={observations.ConsultedWith ?? "-"}.

			This is what gives the abort guard in WriteProblemAsync a purpose: the handler runs on a
			connection that no longer exists. Were handlers not consulted here, the guard would be
			unreachable and should be removed.
		""");
	}

	[Test]
	public async Task FailureOnAnAbortedConnection_ShouldBeRecordedWithoutWritingABody()
	{
		using MemoryStream body = new MemoryStream();

		CapturingLogger<GlobalExceptionHandler> logger = new CapturingLogger<GlobalExceptionHandler>();

		DefaultHttpContext httpContext = HttpContextFactory.Create(body: body, requestAborted: true);

		GlobalExceptionHandler handler = new GlobalExceptionHandler(logger: logger);

		bool handled = await handler.TryHandleAsync(
			httpContext: httpContext,
			exception: new InvalidOperationException(message: "something genuinely broke"),
			cancellationToken: httpContext.RequestAborted
		);

		await Assert.That(value: handled).IsTrue().Because(message: """
			Returning false hands the exception back to the middleware, which rethrows it into a pipeline
			that has nowhere left to send a response.
		""");

		await Assert.That(value: httpContext.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status500InternalServerError).Because(message: """
			The caller being gone does not make the failure any less real. A slow request is exactly what
			makes a caller give up, so a timeout landing after the disconnect is the ordinary shape of
			this — and it has to be recorded as the failure it is.
		""");

		await Assert.That(value: logger.ErrorLogged).IsTrue().Because(message: """
			The response cannot be delivered, but the failure still has to reach the log. Suppressing it
			alongside the body would trade an undeliverable response for an invisible defect.
		""");

		await Assert.That(value: body.Length).IsEqualTo(expected: 0L).Because(message: """
			The token this handler receives from the middleware is RequestAborted itself, already tripped.
			Writing raises a second cancellation before a byte is produced, after which the middleware
			logs that the error handler failed and rethrows the original exception — two error-level
			entries for a response nobody can receive.
		""");
	}

	[Test]
	public async Task FailureOnALiveConnection_ShouldWriteTheProblemDocument()
	{
		using MemoryStream body = new MemoryStream();

		CapturingLogger<GlobalExceptionHandler> logger = new CapturingLogger<GlobalExceptionHandler>();

		DefaultHttpContext httpContext = HttpContextFactory.Create(body: body, requestAborted: false);

		GlobalExceptionHandler handler = new GlobalExceptionHandler(logger: logger);

		bool handled = await handler.TryHandleAsync(
			httpContext: httpContext,
			exception: new InvalidOperationException(message: "something genuinely broke"),
			cancellationToken: httpContext.RequestAborted
		);

		await Assert.That(value: handled).IsTrue();

		await Assert.That(value: body.Length).IsGreaterThan(minimum: 0L).Because(message: """
			This is the assertion that keeps the abort guard honest. A guard written to fire
			unconditionally would satisfy every other test in this class while silently withholding the
			problem document from callers who are still waiting for it.
		""");

		await Assert.That(value: logger.ErrorLogged).IsTrue().Because(message: """
			Nobody disconnected, so this is an ordinary unhandled failure and belongs at error level.
		""");
	}

	private static async Task<Observations> RunDisconnectAsync(bool failAfterAbort)
	{
		CapturingLogger<GlobalExceptionHandler> logger = new CapturingLogger<GlobalExceptionHandler>();

		Observations observations = new Observations();

		TaskCompletionSource requestReached = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

		WebApplication app = BuildApp(
			logger: logger,
			requestReached: requestReached,
			observations: observations,
			failAfterAbort: failAfterAbort
		);

		using HttpClient client = new HttpClient();

		try
		{
			await app.StartAsync();

			string address = app.Urls.First();

			using CancellationTokenSource clientCancellation = new CancellationTokenSource();

			Task request = client.GetAsync(requestUri: $"{address}/hang", cancellationToken: clientCancellation.Token);

			await requestReached.Task.WaitAsync(timeout: ObservationTimeout, cancellationToken: CancellationToken.None);

			await clientCancellation.CancelAsync();

			await DrainAsync(request: request);

			await Task.Delay(delay: SettleWindow);

			return observations;
		}
		finally
		{
			await app.StopAsync();
			await app.DisposeAsync();
		}
	}

	private static WebApplication BuildApp(
		CapturingLogger<GlobalExceptionHandler> logger,
		TaskCompletionSource requestReached,
		Observations observations,
		bool failAfterAbort)
	{
		WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

		builder.WebHost.UseUrls(urls: "http://127.0.0.1:0");

		builder.Services.AddSingleton<ICorrelationContext, CorrelationContext>();
		builder.Services.AddSingleton<ILogger<GlobalExceptionHandler>>(implementationInstance: logger);
		builder.Services.AddSingleton<IExceptionHandler>(implementationInstance: new ProbeExceptionHandler(observations: observations));
		builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
		builder.Services.AddProblemDetails();

		WebApplication app = builder.Build();

		app.Use(middleware: async (httpContext, next) =>
		{
			try
			{
				await next(context: httpContext);
			}
			catch (Exception exception)
			{
				observations.Escaped = exception.GetType().Name;
				throw;
			}
		});

		app.UseExceptionHandler();

		app.MapGet(pattern: "/hang", handler: async (HttpContext httpContext) =>
		{
			requestReached.TrySetResult();

			try
			{
				await Task.Delay(delay: Timeout.InfiniteTimeSpan, cancellationToken: httpContext.RequestAborted);
			}
			catch (OperationCanceledException)
			{
				observations.AbortReachedEndpoint = true;

				if (failAfterAbort)
					throw new InvalidOperationException(message: "failure raised after the caller disconnected");

				throw;
			}

			return Results.Ok();
		});

		return app;
	}

	private static async Task DrainAsync(Task request)
	{
		try
		{
			await request;
		}
		catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException) { }
	}
}
