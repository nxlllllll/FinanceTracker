using System.Reflection;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace FinanceTracker.Tests.Architecture;

public sealed class LayerArchitectureTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;
	private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;
	private static readonly Assembly InfrastructureAssembly = typeof(EFUnitOfWork).Assembly;

	[Test]
	public async Task Core_ShouldNotDependOn_Application()
	{
		TestResult result = Types.InAssembly(assembly: CoreAssembly)
			.ShouldNot().HaveDependencyOnAny("FinanceTracker.Application", "FinanceTracker.Infrastructure")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task Application_ShouldNotDependOn_Infrastructure()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.ShouldNot().HaveDependencyOn(dependency: "FinanceTracker.Infrastructure")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task Infrastructure_ShouldNotDependOn_Application()
	{
		TestResult result = Types.InAssembly(assembly: InfrastructureAssembly)
			.ShouldNot().HaveDependencyOn(dependency: "FinanceTracker.Application")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}
}