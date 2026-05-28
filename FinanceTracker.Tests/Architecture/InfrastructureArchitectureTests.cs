using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace FinanceTracker.Tests.Architecture;

public sealed class InfrastructureArchitectureTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;
	private static readonly Assembly InfrastructureAssembly = typeof(EFUnitOfWork).Assembly;

	[Test]
	public async Task AllRepositoryInterfaces_ShouldResideInCore()
	{
		TestResult result = Types.InAssembly(assembly: CoreAssembly)
			.That().HaveNameStartingWith(start: "I")
			.And().HaveNameEndingWith(end: "Repository")
			.Should().ResideInNamespaceStartingWith(name: "FinanceTracker.Core.Repositories")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllRepositoryImplementations_ShouldResideInInfrastructure()
	{
		TestResult result = Types.InAssembly(assembly: InfrastructureAssembly)
			.That().HaveNameEndingWith(end: "Repository")
			.And().AreNotInterfaces()
			.Should().ResideInNamespaceStartingWith(name: "FinanceTracker.Infrastructure")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllRepositoryImplementations_ShouldBeSealed()
	{
		TestResult result = Types.InAssembly(assembly: InfrastructureAssembly)
			.That().HaveNameEndingWith(end: "Repository")
			.And().AreClasses()
			.Should().BeSealed()
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}
}