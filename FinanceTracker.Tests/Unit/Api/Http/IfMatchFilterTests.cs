using System.Text.Json;
using FinanceTracker.Api.Http.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.Api.Http;

public sealed class IfMatchFilterTests
{
	private static readonly object Sentinel = new object();

	private static DefaultHttpContext ContextWithBody(out MemoryStream body)
	{
		body = new MemoryStream();

		FeatureCollection features = new FeatureCollection();
		features.Set<IHttpRequestFeature>(instance: new HttpRequestFeature());
		features.Set<IHttpResponseFeature>(instance: new HttpResponseFeature());
		features.Set<IHttpResponseBodyFeature>(instance: new StreamResponseBodyFeature(stream: body));

		return new DefaultHttpContext(features: features)
		{
			RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
		};
	}

	private static async Task<object?> InvokeAsync(string? ifMatch)
	{
		DefaultHttpContext httpContext = new DefaultHttpContext
		{
			RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
		};

		if (ifMatch is not null)
			httpContext.Request.Headers.IfMatch = ifMatch;

		return await new IfMatchFilter().InvokeAsync(
			context: EndpointFilterInvocationContext.Create(httpContext: httpContext),
			next: _ => ValueTask.FromResult<object?>(result: Sentinel)
		);
	}

	[Test]
	public async Task InvokeAsync_WithReadablePrecondition_ShouldReachTheHandler()
		=> await Assert.That(value: await InvokeAsync(ifMatch: "\"7\"")).IsSameReferenceAs(expected: Sentinel);

	[Test]
	public async Task InvokeAsync_WithWildcard_ShouldReachTheHandler()
		=> await Assert.That(value: await InvokeAsync(ifMatch: "*")).IsSameReferenceAs(expected: Sentinel);

	[Test]
	public async Task InvokeAsync_WithNoHeader_ShouldReachTheHandler()
	{
		await Assert.That(value: await InvokeAsync(ifMatch: null)).IsSameReferenceAs(expected: Sentinel)
			.Because(message: "the precondition is optional, and refusing its absence would break every unconditional write");
	}

	[Test]
	public async Task InvokeAsync_WithUnusableValue_ShouldAnswerWithoutRunningTheHandler()
	{
		object? result = await InvokeAsync(ifMatch: "W/\"7\"");

		await Assert.That(value: result).IsNotSameReferenceAs(expected: Sentinel)
			.Because(message: "letting a refused precondition through would apply the write the client meant to guard");
	}

	[Test]
	public async Task InvokeAsync_WithUnusableValue_ShouldNameTheHeaderItRejected()
	{
		object? result = await InvokeAsync(ifMatch: "W/\"7\"");

		DefaultHttpContext responseContext = ContextWithBody(body: out MemoryStream body);

		await ((IResult)result!).ExecuteAsync(httpContext: responseContext);

		await Assert.That(value: responseContext.Response.StatusCode).IsEqualTo(expected: StatusCodes.Status400BadRequest);

		using JsonDocument problem = JsonDocument.Parse(utf8Json: body.ToArray());

		await Assert.That(value: problem.RootElement.GetProperty(propertyName: "errors").TryGetProperty(propertyName: "ifMatch", value: out _))
			.IsTrue()
			.Because(message: "a validation problem without the offending field name leaves the client guessing which header to fix");
	}
}
