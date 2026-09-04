using System.Reflection;
using FinanceTracker.Core.Services.Rebuild;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Infrastructure.Services.Rebuild;

/// <summary>
/// Resolves a projection by the name on its <see cref="ProjectionAttribute"/>.
/// </summary>
public sealed class ProjectionRegistry(IServiceProvider serviceProvider)
{
	private static readonly IReadOnlyDictionary<string, Type> ByName = ProjectionCatalog.Discover(
		assemblies: [Assembly.GetExecutingAssembly()]
	);

	public static IReadOnlyCollection<string> Names => ByName.Keys.Order().ToArray();

	/// <summary>
	/// Resolves the projection registered under <paramref name="name"/>, or <c>null</c> when there is none
	/// </summary>
	public (IProjectionRebuild Projection, string AggregateType)? Resolve(string name)
	{
		if (!ByName.TryGetValue(key: name, out Type? type))
			return null;

		IProjectionRebuild projection = (IProjectionRebuild)serviceProvider.GetRequiredService(serviceType: type);

		return (projection, ProjectionCatalog.AggregateTypeOf(projectionType: type));
	}

	public static string? AggregateTypeOfName(string name)
		=> ByName.TryGetValue(key: name, out Type? type) ? ProjectionCatalog.AggregateTypeOf(projectionType: type) : null;
}
