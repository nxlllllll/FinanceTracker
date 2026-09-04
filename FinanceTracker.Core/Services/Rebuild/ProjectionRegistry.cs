using System.Reflection;

namespace FinanceTracker.Core.Services.Rebuild;

/// <summary>
/// Maps a projection name to the type that implements it.
/// </summary>
public static class ProjectionCatalog
{
	public static IReadOnlyDictionary<string, Type> Discover(params Assembly[] assemblies)
	{
		Dictionary<string, Type> byName = new Dictionary<string, Type>(comparer: StringComparer.OrdinalIgnoreCase);

		IEnumerable<Type> implementations = assemblies
			.SelectMany(selector: assembly => assembly.GetTypes())
			.Where(predicate: type => type is { IsClass: true, IsAbstract: false } && typeof(IProjectionRebuild).IsAssignableFrom(c: type));

		foreach (Type type in implementations)
		{
			ProjectionAttribute attribute = type.GetCustomAttribute<ProjectionAttribute>() ?? throw new InvalidOperationException(
				message: $"{type.Name} implements {nameof(IProjectionRebuild)} without a [{nameof(ProjectionAttribute)}], so nothing can name it."
			);

			if (byName.TryGetValue(key: attribute.Name, out Type? existing))
				throw new InvalidOperationException(message: $"Both {existing.Name} and {type.Name} claim the projection name '{attribute.Name}'.");

			byName[attribute.Name] = type;
		}

		return byName;
	}

	public static string AggregateTypeOf(Type projectionType)
	{
		return projectionType.GetCustomAttribute<ProjectionAttribute>()?.AggregateType ?? throw new InvalidOperationException(
			message: $"{projectionType.Name} has no [{nameof(ProjectionAttribute)}]."
		);
	}
}
