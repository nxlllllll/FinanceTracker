using FinanceTracker.Api;
using FinanceTracker.Api.Configurations;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Routing;
using FinanceTracker.Infrastructure.Services.Token;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Architecture;

/// <summary>
/// Every mapped endpoint must declare a specific authorization policy: either a permission or the root policy.
/// </summary>
public sealed class EndpointAuthorizationArchitectureTests
{
	// Full route as served, with the reason it needs no permission or root policy.
	private static readonly HashSet<string> ExemptRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"POST /api/v1/auth/login",    // account login cannot be authenticated
		"POST /api/v1/auth/register", // the registration of a new user cannot be authenticated
		"POST /api/v1/auth/refresh",  // identifies the caller via the refresh-token cookie
		"POST /api/v1/auth/logout",   // identifies the caller via the refresh-token cookie
	};

	private static IEnumerable<T> InstancesOf<T>()
	{
		return typeof(Program).Assembly.GetTypes()
							.Where(predicate: type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(targetType: typeof(T)))
							.Select(selector: type => (T)Activator.CreateInstance(type: type)!);
	}

	private static IEndpointRouteBuilder MapEverything()
	{
		WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

		builder.Services.AddSingleton(implementationInstance: Substitute.For<ICurrentUserProvider>());
		builder.Services.AddSingleton(implementationInstance: Substitute.For<ISender>());
		builder.Services.AddSingleton(implementationInstance: Options.Create(options: new JwtOptions()));

		WebApplication app = builder.Build();
		IEndpointRouteBuilder routeBuilder = app;

		routeBuilder.MapEndpoints(
			groups: InstancesOf<IEndpointGroup>(),
			endpoints: InstancesOf<IEndpoint>(),
			options: new ApiRoutingOptions()
		);

		return routeBuilder;
	}

	private static IEnumerable<(string Route, RouteEndpoint Endpoint)> MappedRoutes(IEndpointRouteBuilder routeBuilder)
	{
		foreach (Endpoint mapped in routeBuilder.DataSources.SelectMany(selector: ds => ds.Endpoints))
		{
			if (mapped is not RouteEndpoint routeEndpoint)
				continue;

			IReadOnlyList<string>? httpMethods = routeEndpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
			string httpMethod = httpMethods is { Count: > 0 } ? httpMethods[0] : "?";

			yield return ($"{httpMethod} /{routeEndpoint.RoutePattern.RawText?.TrimStart('/')}", routeEndpoint);
		}
	}

	[Test]
	public async Task EveryEndpoint_ShouldRequireAPermissionOrRootPolicy_UnlessExplicitlyExempt()
	{
		List<string> violations = [];

		foreach ((string route, RouteEndpoint endpoint) in MappedRoutes(routeBuilder: MapEverything()))
		{
			if (ExemptRoutes.Contains(item: route))
				continue;

			IReadOnlyList<IAuthorizeData> authData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
			bool hasSpecificPolicy = authData.Any(predicate: a =>
				a.Policy is not null &&
				(a.Policy.StartsWith(value: "permission:", comparisonType: StringComparison.OrdinalIgnoreCase) || a.Policy == "root")
			);

			if (!hasSpecificPolicy)
				violations.Add(item: route);
		}

		await Assert.That(value: violations).IsEmpty().Because(message:
			$"These endpoints have no permission/root policy — add RequirePermission(...)/RequireRoot(), or list in " +
			$"{nameof(ExemptRoutes)} with a reason: {String.Join(separator: ", ", values: violations)}"
		);
	}

	[Test]
	public async Task ExemptRoutes_ShouldAllStillExist()
	{
		HashSet<string> mapped = MappedRoutes(routeBuilder: MapEverything())
			.Select(selector: x => x.Route)
			.ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);

		List<string> stale = ExemptRoutes.Where(predicate: route => !mapped.Contains(item: route)).ToList();

		await Assert.That(value: stale).IsEmpty().Because(message:
			$"{nameof(ExemptRoutes)} lists routes that are not mapped anymore — remove them or fix the path: " +
			$"{String.Join(separator: ", ", values: stale)}"
		);
	}
}
