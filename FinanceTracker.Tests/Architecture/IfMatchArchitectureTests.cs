using FinanceTracker.Api;
using FinanceTracker.Api.Configurations;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Http.Filters;
using FinanceTracker.Api.Routing;
using FinanceTracker.Api.Security;
using FinanceTracker.Infrastructure.Services.Token;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Architecture;

/// <summary>
/// Every endpoint that honours <c>If-Match</c> must also validate it.
/// </summary>
public sealed class IfMatchArchitectureTests
{
	private static readonly HashSet<string> ConditionalRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"PATCH /api/v1/accounts/{accountId:guid}/rename",
		"PATCH /api/v1/accounts/{accountId:guid}/archive",
		"PATCH /api/v1/accounts/{accountId:guid}/unarchive",
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

	private static IEnumerable<(string Route, RouteEndpoint Endpoint)> MappedRoutes()
	{
		foreach (Endpoint mapped in MapEverything().DataSources.SelectMany(selector: ds => ds.Endpoints))
		{
			if (mapped is not RouteEndpoint routeEndpoint)
				continue;

			IReadOnlyList<string>? httpMethods = routeEndpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
			string httpMethod = httpMethods is { Count: > 0 } ? httpMethods[0] : "?";

			yield return ($"{httpMethod} /{routeEndpoint.RoutePattern.RawText?.TrimStart('/')}", routeEndpoint);
		}
	}

	[Test]
	public async Task EveryConditionalRoute_ShouldValidateIfMatch()
	{
		List<string> violations = MappedRoutes().Where(predicate: x => ConditionalRoutes.Contains(item: x.Route))
			.Where(predicate: x => x.Endpoint.Metadata.GetMetadata<AcceptsIfMatchMetadata>() is null)
			.Select(selector: x => x.Route)
			.ToList();

		await Assert.That(value: violations).IsEmpty().Because(message:
			$"These routes accept an If-Match header but never check whether it can be evaluated, so a " +
			$"value the parser does not understand is treated as no precondition at all and the write " +
			$"goes through unguarded. Add {nameof(AuthorizationExtensions.AcceptsIfMatch)}(): " +
			$"{String.Join(separator: ", ", values: violations)}"
		);
	}

	[Test]
	public async Task NoOtherRoute_ShouldClaimToValidateIfMatch()
	{
		List<string> violations = MappedRoutes().Where(predicate: x => !ConditionalRoutes.Contains(item: x.Route))
			.Where(predicate: x => x.Endpoint.Metadata.GetMetadata<AcceptsIfMatchMetadata>() is not null)
			.Select(selector: x => x.Route)
			.ToList();

		await Assert.That(value: violations).IsEmpty().Because(message:
			$"These routes validate If-Match but are not listed as conditional. Either they started " +
			$"honouring a precondition and belong in {nameof(ConditionalRoutes)}, or the filter is " +
			$"rejecting headers on an endpoint that ignores them anyway: " +
			$"{String.Join(separator: ", ", values: violations)}"
		);
	}

	[Test]
	public async Task ConditionalRoutes_ShouldAllStillExist()
	{
		HashSet<string> mapped = MappedRoutes().Select(selector: x => x.Route).ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);

		List<string> stale = ConditionalRoutes.Where(predicate: route => !mapped.Contains(item: route)).ToList();

		await Assert.That(value: stale).IsEmpty().Because(message:
			$"{nameof(ConditionalRoutes)} lists routes that are not mapped anymore — remove them or fix " +
			$"the path: {String.Join(separator: ", ", values: stale)}"
		);
	}
}
