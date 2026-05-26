using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace FinanceTracker.Tests.Architecture;

public sealed class DomainArchitectureTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;

	[Test]
	public async Task AllDomainClasses_ShouldBeSealed()
	{
		TestResult result = Types.InAssembly(assembly: CoreAssembly)
			.That().ResideInNamespaceStartingWith(name: "FinanceTracker.Core.Domains")
			.And().AreClasses()
			.And().AreNotAbstract()
			.Should().BeSealed()
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task Account_ShouldHandleAllAccountEvents_InApplyMethod()
	{
		Type[] accountEventTypes = CoreAssembly.GetTypes()
			.Where(predicate: t => typeof(IEvent).IsAssignableFrom(c: t) && t is
			{
				IsClass: true,
				IsAbstract: false,
				Namespace: "FinanceTracker.Core.Domains.Account.Events"
			}).ToArray();

		HashSet<Type> handledTypes = typeof(Account)
			.GetMethods(bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance)
			.Where(predicate: m => m.Name == "Apply"
				&& m.GetParameters().Length == 1
				&& typeof(IEvent).IsAssignableFrom(c: m.GetParameters()[0].ParameterType))
			.Select(selector: m => m.GetParameters()[0].ParameterType)
			.ToHashSet();

		Type[] unhandled = accountEventTypes
			.Where(predicate: e => !handledTypes.Contains(item: e))
			.ToArray();

		await Assert.That(value: unhandled).IsEmpty()
			.Because(message: $"Account.Apply() is missing handlers for: {String.Join(separator: ", ", values: unhandled.Select(selector: t => t.Name))}");
	}
}
