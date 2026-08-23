using FinanceTracker.Core.Observability.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class HttpContextFactory
{
	public static DefaultHttpContext Create(
		Stream body,
		bool requestAborted = false,
		string method = "GET",
		string path = "/api/v1/accounts",
		Guid? correlationId = null)
	{
		ICorrelationContext correlationContext = Substitute.For<ICorrelationContext>();
		correlationContext.CorrelationId.Returns(returnThis: correlationId ?? Guid.CreateVersion7());

		ServiceProvider services = new ServiceCollection()
			.AddSingleton(implementationInstance: correlationContext)
			.BuildServiceProvider();

		DefaultHttpContext httpContext = new DefaultHttpContext
		{
			RequestServices = services
		};

		httpContext.Request.Method = method;
		httpContext.Request.Path = path;
		httpContext.Response.Body = body;

		CancellationTokenSource abortSource = new CancellationTokenSource();

		if (requestAborted)
			abortSource.Cancel();

		httpContext.RequestAborted = abortSource.Token;

		return httpContext;
	}
}
