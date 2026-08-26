using FinanceTracker.Api.Http.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration.Api;

public sealed class SecurityHeadersPipelineTests
{
	private static readonly HttpClient Client = new HttpClient();

	private sealed class ProblemExceptionHandler : IExceptionHandler
	{
		public async ValueTask<bool> TryHandleAsync(
			HttpContext httpContext,
			Exception exception,
			CancellationToken cancellationToken)
		{
			httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
			httpContext.Response.ContentType = "application/problem+json";

			await httpContext.Response.WriteAsync(text: """{"title":"An unexpected error occurred."}""", cancellationToken: cancellationToken);

			return true;
		}
	}

	private static async Task<HttpResponseMessage> RequestAsync(string path)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		builder.WebHost.UseUrls(urls: "http://127.0.0.1:0");

		builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
		builder.Services.AddProblemDetails();

		await using WebApplication app = builder.Build();

		app.UseSecurityHeaders();
		app.UseExceptionHandler();

		app.MapGet(pattern: "/fine", handler: () => Results.Text(content: "ok"));
		app.MapGet(pattern: "/boom", handler: void () => throw new InvalidOperationException(message: "boom"));

		await app.StartAsync();

		string baseAddress = app.Services.GetRequiredService<IServer>()
			.Features.Get<IServerAddressesFeature>()!
			.Addresses.First();

		HttpResponseMessage response = await Client.GetAsync(requestUri: $"{baseAddress}{path}");

		await response.Content.ReadAsStringAsync();

		await app.StopAsync();

		return response;
	}

	[Test]
	public async Task SuccessfulResponse_ShouldCarryTheSecurityHeaders()
	{
		HttpResponseMessage response = await RequestAsync(path: "/fine");

		await Assert.That(value: (int)response.StatusCode).IsEqualTo(expected: 200);
		await Assert.That(value: response.Headers.Contains(name: "Content-Security-Policy")).IsTrue();
		await Assert.That(value: response.Headers.Contains(name: "X-Content-Type-Options")).IsTrue();
	}

	[Test]
	public async Task FailingResponse_ShouldStillCarryTheSecurityHeaders()
	{
		HttpResponseMessage response = await RequestAsync(path: "/boom");

		await Assert.That(value: (int)response.StatusCode).IsEqualTo(expected: 500).Because(message: """
			Without a genuine 500 the rest of this test is vacuous — it would be measuring the ordinary path
			the sibling test already covers.
		""");

		await Assert.That(value: response.Headers.Contains(name: "Content-Security-Policy")).IsTrue().Because(message: """
			ExceptionHandlerMiddleware calls Response.Clear() before handing the failure to a handler, so any
			header written on the way in is gone by the time the body is produced. Error pages are the ones a
			browser is most likely to render with whatever the server echoed back, which is exactly when a
			content-security policy earns its keep.
		""");

		await Assert.That(value: response.Headers.Contains(name: "X-Content-Type-Options")).IsTrue();
		await Assert.That(value: response.Headers.GetValues(name: "Referrer-Policy").Single()).IsEqualTo(expected: "no-referrer");
	}
}
