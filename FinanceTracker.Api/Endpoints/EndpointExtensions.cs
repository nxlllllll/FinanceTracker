using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinanceTracker.Api.Endpoints;

/// <summary>
/// Assembly-scanning registration and startup mapping for <see cref="IEndpoint"/> implementations.
/// </summary>
public static class EndpointExtensions
{
	/// <summary>
	/// Discovers every non-abstract <see cref="IEndpoint"/> implementation
	/// in the API assembly and registers it as a transient service
	/// </summary>
	public static IServiceCollection AddEndpoints(this IServiceCollection services)
	{
		ServiceDescriptor[] descriptors = typeof(Program).Assembly.GetTypes()
			.Where(predicate: type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(targetType: typeof(IEndpoint)))
			.Select(selector: type => ServiceDescriptor.Transient(service: typeof(IEndpoint), implementationType: type))
			.ToArray();

		services.TryAddEnumerable(descriptors: descriptors);

		return services;
	}

	/// <summary>Resolves all registered endpoints and maps their routes.</summary>
	public static WebApplication MapEndpoints(this WebApplication app)
	{
		IEnumerable<IEndpoint> endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();
		foreach (IEndpoint endpoint in endpoints)
			endpoint.MapEndpoint(app: app);

		return app;
	}
}
