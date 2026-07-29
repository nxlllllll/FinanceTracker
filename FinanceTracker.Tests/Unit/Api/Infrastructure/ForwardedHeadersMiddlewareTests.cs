using System.Net;
using FinanceTracker.Api.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class ForwardedHeadersMiddlewareTests
{
	private static async Task<HttpContext> InvokeAsync(
		ProxyOptions proxy,
		IPAddress remotePeer,
		string? forwardedFor)
	{
		ForwardedHeadersOptions options = new ForwardedHeadersOptions();
		new ForwardedHeadersOptionsSetup(proxyOptions: Options.Create(options: proxy)).Configure(options: options);

		ForwardedHeadersMiddleware middleware = new ForwardedHeadersMiddleware(
			next: _ => Task.CompletedTask,
			loggerFactory: NullLoggerFactory.Instance,
			options: Options.Create(options: options)
		);

		DefaultHttpContext context = new DefaultHttpContext
		{
			Connection = { RemoteIpAddress = remotePeer }
		};
		if (forwardedFor is not null)
			context.Request.Headers["X-Forwarded-For"] = forwardedFor;

		await middleware.Invoke(context: context);
		return context;
	}

	[Test]
	public async Task WithTrustedProxyNetwork_ShouldRewriteRemoteIpAddressToTheForwardedValue()
	{
		HttpContext context = await InvokeAsync(
			proxy: new ProxyOptions { KnownNetworks = ["10.0.0.0/8"] },
			remotePeer: IPAddress.Parse(ipString: "10.0.0.5"),
			forwardedFor: "203.0.113.7"
		);

		await Assert.That(value: context.Connection.RemoteIpAddress).IsEqualTo(expected: IPAddress.Parse(ipString: "203.0.113.7"));
	}

	[Test]
	public async Task WithUntrustedConnectingPeer_ShouldIgnoreTheHeader()
	{
		IPAddress untrustedPeer = IPAddress.Parse(ipString: "192.168.1.1");

		HttpContext context = await InvokeAsync(
			proxy: new ProxyOptions { KnownNetworks = ["10.0.0.0/8"] },
			remotePeer: untrustedPeer,
			forwardedFor: "203.0.113.7"
		);

		await Assert.That(value: context.Connection.RemoteIpAddress).IsEqualTo(expected: untrustedPeer);
	}

	[Test]
	public async Task WithNoProxyConfiguration_ShouldNotTrustANonLoopbackPeer()
	{
		IPAddress dockerBridgePeer = IPAddress.Parse(ipString: "172.20.0.3");

		HttpContext context = await InvokeAsync(
			proxy: new ProxyOptions(),
			remotePeer: dockerBridgePeer,
			forwardedFor: "203.0.113.7"
		);

		await Assert.That(value: context.Connection.RemoteIpAddress).IsEqualTo(expected: dockerBridgePeer);
	}

	[Test]
	public async Task WithNoForwardedForHeader_ShouldLeaveRemoteIpAddressUnchanged()
	{
		IPAddress peer = IPAddress.Parse(ipString: "10.0.0.5");

		HttpContext context = await InvokeAsync(
			proxy: new ProxyOptions { KnownNetworks = ["10.0.0.0/8"] },
			remotePeer: peer,
			forwardedFor: null
		);

		await Assert.That(value: context.Connection.RemoteIpAddress).IsEqualTo(expected: peer);
	}
}
