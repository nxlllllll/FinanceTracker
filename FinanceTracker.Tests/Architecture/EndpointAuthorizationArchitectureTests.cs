using FinanceTracker.Api.Endpoints;
using FinanceTracker.Api.Infrastructure;
using FinanceTracker.Infrastructure.Services.Token;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Architecture;

/// <summary>
/// Every mapped endpoint must declare a specific authorization
/// policy: either a permission or the root policy.
/// </summary>
public sealed class EndpointAuthorizationArchitectureTests
{
	// method + route pattern (exactly as passed to Map*), with the reason it doesn't need a
	// permission/root policy.
	private static readonly HashSet<string> ExemptRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"POST /auth/login",    // account login cannot be authenticated
		"POST /auth/register", // the registration of a new user cannot be authenticated
		"POST /auth/refresh",  // identifies the caller via the refresh-token cookie
		"POST /auth/logout",   // identifies the caller via the refresh-token cookie
	};

	[Test]
	public async Task EveryEndpoint_ShouldRequireAPermissionOrRootPolicy_UnlessExplicitlyExempt()
	{
		WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

		builder.Services.AddSingleton(implementationInstance: Substitute.For<ICurrentUserProvider>());
		builder.Services.AddSingleton(implementationInstance: Substitute.For<ISender>());
		builder.Services.AddSingleton(implementationInstance: Options.Create(options: new JwtOptions()));

		WebApplication app = builder.Build();
		IEndpointRouteBuilder routeBuilder = app;

		IEnumerable<IEndpoint> endpoints = typeof(Api.Program).Assembly.GetTypes()
			.Where(predicate: type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(targetType: typeof(IEndpoint)))
			.Select(selector: type => (IEndpoint)Activator.CreateInstance(type: type)!);

		foreach (IEndpoint endpoint in endpoints)
			endpoint.MapEndpoint(app: routeBuilder);

		List<string> violations = new List<string>();

		foreach (Endpoint mapped in routeBuilder.DataSources.SelectMany(selector: ds => ds.Endpoints))
		{
			if (mapped is not RouteEndpoint routeEndpoint)
				continue;

			IReadOnlyList<string>? httpMethods = routeEndpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
			string httpMethod = httpMethods is { Count: > 0 } ? httpMethods[0] : "?";
			string route = $"{httpMethod} {routeEndpoint.RoutePattern.RawText}";

			if (ExemptRoutes.Contains(item: route))
				continue;

			IReadOnlyList<IAuthorizeData> authData = routeEndpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
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
}
