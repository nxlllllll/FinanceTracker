using FinanceTracker.Api.Configurations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Routing;

/// <summary>
/// Assembly-scanning registration and startup mapping for <see cref="IEndpoint"/> and
/// <see cref="IEndpointGroup"/> implementations.
/// </summary>
public static class EndpointExtensions
{
	public static IServiceCollection AddEndpoints(this IServiceCollection services)
	{
		services.AddOptions<ApiRoutingOptions>()
			.BindConfiguration(configSectionPath: ApiRoutingOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		Type[] types = typeof(Program).Assembly.GetTypes().Where(predicate: type => type is { IsAbstract: false, IsInterface: false }).ToArray();

		services.TryAddEnumerable(descriptors: Describe<IEndpoint>(types: types));
		services.TryAddEnumerable(descriptors: Describe<IEndpointGroup>(types: types));

		return services;
	}

	private static ServiceDescriptor[] Describe<TService>(Type[] types)
	{
		return types.Where(predicate: type => type.IsAssignableTo(targetType: typeof(TService)))
			.Select(selector: type => ServiceDescriptor.Transient(service: typeof(TService), implementationType: type))
			.ToArray();
	}

	public static WebApplication MapEndpoints(this WebApplication app)
	{
		app.MapEndpoints(
			groups: app.Services.GetRequiredService<IEnumerable<IEndpointGroup>>(),
			endpoints: app.Services.GetRequiredService<IEnumerable<IEndpoint>>(),
			options: app.Services.GetRequiredService<IOptions<ApiRoutingOptions>>().Value
		);

		return app;
	}

	/// <summary>
	/// The composition itself, kept free of service resolution so tests can exercise the real
	/// arrangement of groups and endpoints without standing up a host.
	/// </summary>
	public static void MapEndpoints(
		this IEndpointRouteBuilder root,
		IEnumerable<IEndpointGroup> groups,
		IEnumerable<IEndpoint> endpoints,
		ApiRoutingOptions options)
	{
		List<IEndpoint> all = [.. endpoints];
		Dictionary<string, IEndpointGroup> byName = IndexGroups(groups: groups, endpoints: all);
		RouteGroupBuilder api = MapApiRoot(root: root, options: options);

		foreach (IGrouping<string, IEndpoint> grouped in all.GroupBy(keySelector: endpoint => endpoint.GroupName))
			MapGroup(api: api, group: byName[key: grouped.Key], endpoints: grouped);
	}

	/// <summary>Indexes groups by name, rejecting endpoints that point at one that does not exist.</summary>
	private static Dictionary<string, IEndpointGroup> IndexGroups(
		IEnumerable<IEndpointGroup> groups,
		IReadOnlyList<IEndpoint> endpoints)
	{
		Dictionary<string, IEndpointGroup> byName = groups.ToDictionary(keySelector: group => group.Name);

		List<string> orphans = endpoints.Where(predicate: endpoint => !byName.ContainsKey(key: endpoint.GroupName))
			.Select(selector: endpoint => $"{endpoint.GetType().Name} -> '{endpoint.GroupName}'")
			.ToList();

		if (orphans.Count == 0)
			return byName;

		throw new InvalidOperationException(message:
			$"These endpoints name a group that does not exist: {String.Join(separator: ", ", values: orphans)}. " +
			$"Known groups: {String.Join(separator: ", ", values: byName.Keys.Order())}."
		);
	}

	/// <summary>Opens the versioned root every group hangs off, with the responses common to all of them.</summary>
	private static RouteGroupBuilder MapApiRoot(
		IEndpointRouteBuilder root,
		ApiRoutingOptions options)
	{
		return root.MapGroup(prefix: options.Prefix).RequireAuthorization()
			.ProducesProblem(statusCode: StatusCodes.Status401Unauthorized)
			.ProducesProblem(statusCode: StatusCodes.Status403Forbidden)
			.ProducesProblem(statusCode: StatusCodes.Status429TooManyRequests);
	}

	private static void MapGroup(
		RouteGroupBuilder api,
		IEndpointGroup group,
		IEnumerable<IEndpoint> endpoints)
	{
		RouteGroupBuilder builder = api.MapGroup(prefix: group.Prefix);
		group.Configure(group: builder);

		foreach (IEndpoint endpoint in endpoints)
			endpoint.MapEndpoint(group: builder);
	}
}
