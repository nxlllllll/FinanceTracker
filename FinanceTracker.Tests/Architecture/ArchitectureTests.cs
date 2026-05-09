using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace FinanceTracker.Tests.Architecture;

public sealed class ArchitectureTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;
	private static readonly Assembly ApplicationAssembly = typeof(Application.Configurations.DependencyInjection).Assembly;
	private static readonly Assembly InfrastructureAssembly = typeof(EFUnitOfWork).Assembly;

	private static readonly string[] SystemAssemblyPrefixes =
	[
		"System",
		"Microsoft.NETCore",
		"Microsoft.CSharp",
		"mscorlib",
		"netstandard"
	];

	private static readonly string[] InfrastructureAllowedAssemblyPrefixes =
	[
		..SystemAssemblyPrefixes,
		"FinanceTracker",
		"Microsoft.EntityFrameworkCore",
		"Npgsql",
		"Quartz",
		"Microsoft.Extensions",
		"ZLogger"
	];

	[Test]
	public async Task Core_ShouldNotDependOnAnyThirdPartyPackages()
	{
		IEnumerable<string> violations = CoreAssembly
			.GetReferencedAssemblies()
			.Select(selector: a => a.Name ?? string.Empty)
			.Where(predicate: name => !SystemAssemblyPrefixes.Any(
				predicate: prefix => name.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase)
			));

		await Assert.That(value: violations).IsEmpty()
			.Because(message: String.Join(separator: ", ", values: violations));
	}

	[Test]
	public async Task Infrastructure_ShouldOnlyDependOnAllowedPackages()
	{
		IEnumerable<string> violations = InfrastructureAssembly
			.GetReferencedAssemblies()
			.Select(selector: a => a.Name ?? string.Empty)
			.Where(predicate: name => !InfrastructureAllowedAssemblyPrefixes.Any(
				predicate: prefix => name.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase)
			));

		await Assert.That(value: violations).IsEmpty()
			.Because(message: String.Join(separator: ", ", values: violations));
	}

	[Test]
	public async Task AllIEventClasses_ShouldHaveEventTypeAttribute()
	{
		TestResult result = Types.InAssembly(assembly: CoreAssembly)
			.That()
			.ImplementInterface(interfaceType: typeof(IEvent))
			.And()
			.AreClasses()
			.Should()
			.HaveCustomAttribute(attribute: typeof(EventTypeAttribute))
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllIRequestHandlerClasses_ShouldHaveHandlerSuffix()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That()
			.ImplementInterface(interfaceType: typeof(MediatR.IRequestHandler<,>))
			.And()
			.DoNotHaveNameStartingWith(start: "AuthorizedHandlerAdapter")
			.Should()
			.HaveNameEndingWith(end: "Handler")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllIValidatorClasses_ShouldHaveValidatorSuffix()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That()
			.ImplementInterface(interfaceType: typeof(FluentValidation.IValidator<>))
			.Should()
			.HaveNameEndingWith(end: "Validator")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllDomainClasses_ShouldBeSealed()
	{
		TestResult result = Types.InAssembly(assembly: CoreAssembly)
			.That()
			.ResideInNamespaceStartingWith(name: "FinanceTracker.Core.Domains")
			.And()
			.AreClasses()
			.And()
			.AreNotAbstract()
			.Should()
			.BeSealed()
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task Application_UseCases_ShouldResideInUseCasesNamespace()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That()
			.HaveNameEndingWith(end: "Handler")
			.Or()
			.HaveNameEndingWith(end: "Loader")
			.Should()
			.ResideInNamespaceStartingWith(name: "FinanceTracker.Application.UseCases")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}
}