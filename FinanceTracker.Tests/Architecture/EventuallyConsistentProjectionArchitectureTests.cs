using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Repositories.Abstractions;

namespace FinanceTracker.Tests.Architecture;

public sealed class EventuallyConsistentProjectionArchitectureTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;

	private static IEnumerable<MethodInfo> EventFedWriteRepositoryMethods()
	{
		return CoreAssembly.GetTypes()
			.Where(predicate: t => t.IsInterface && t.Name.EndsWith(value: "WriteRepository", comparisonType: StringComparison.Ordinal))
			.SelectMany(selector: t => t.GetMethods())
			.Where(predicate: m => m.GetParameters().Any(predicate: p => typeof(IEvent).IsAssignableFrom(c: p.ParameterType)));
	}

	[Test]
	public async Task EventFedWriteRepositoryMethods_ShouldDeclareExactlyOneSafetyStrategy()
	{
		List<string> violations = [];

		foreach (MethodInfo method in EventFedWriteRepositoryMethods())
		{
			int strategyCount = method.GetCustomAttributes(inherit: false).Count(predicate: a => a is IEventProjectionSafetyAttribute);

			if (strategyCount != 1)
				violations.Add(item: $"{method.DeclaringType!.Name}.{method.Name} has {strategyCount} [EventuallyConsistent*] attributes (expected exactly 1).");
		}

		await Assert.That(value: violations.Count).IsEqualTo(expected: 0)
			.Because(message: String.Join(separator: "\n", values: violations));
	}

	[Test]
	public async Task EventuallyConsistentAssignmentMethods_ShouldHaveAnOutOfOrderRegressionTest()
	{
		Assembly testsAssembly = typeof(EventuallyConsistentProjectionArchitectureTests).Assembly;

		HashSet<string> testMethodNames = testsAssembly.GetTypes()
			.SelectMany(selector: t => t.GetMethods(bindingAttr: BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			.Select(selector: m => m.Name)
			.ToHashSet();

		List<string> violations = [];

		foreach (MethodInfo method in EventFedWriteRepositoryMethods())
		{
			bool isAssignment = method.GetCustomAttributes(inherit: false).Any(predicate: a => a is EventuallyConsistentAssignmentAttribute);

			if (!isAssignment)
				continue;

			bool hasRegressionTest = testMethodNames.Any(predicate: name =>
				name.StartsWith(value: method.Name, comparisonType: StringComparison.Ordinal) &&
				name.Contains(value: "OutOfOrder", comparisonType: StringComparison.Ordinal)
			);

			if (!hasRegressionTest)
				violations.Add(item: $"{method.DeclaringType!.Name}.{method.Name} is [EventuallyConsistentAssignment] but no test named '{method.Name}*OutOfOrder*' was found.");
		}

		await Assert.That(value: violations.Count).IsEqualTo(expected: 0)
			.Because(message: String.Join(separator: "\n", values: violations));
	}
}
