using System.Reflection;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace FinanceTracker.Tests.Architecture;

public sealed class AssemblyDependencyTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;
	private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;
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
		"ZLogger",
		"Scrutor",
		"Konscious.Security.Cryptography",
		"Microsoft.IdentityModel.Tokens",
		"Blake3",
		"StackExchange.Redis",
		"Microsoft.IdentityModel.JsonWebTokens",
		"HealthChecks"
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
	public async Task Core_ShouldNotDependOnInfrastructure()
	{
		TestResult result = Types.InAssembly(assembly: CoreAssembly)
			.ShouldNot().HaveDependencyOn(dependency: "FinanceTracker.Infrastructure")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task Application_ShouldNotDependOnInfrastructure()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.ShouldNot().HaveDependencyOn(dependency: "FinanceTracker.Infrastructure")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task Infrastructure_ShouldOnlyDependOnAllowedPackages()
	{
		IEnumerable<string> violations = InfrastructureAssembly
			.GetReferencedAssemblies()
			.Select(selector: a => a.Name ?? String.Empty)
			.Where(predicate: name => !InfrastructureAllowedAssemblyPrefixes.Any(
				predicate: prefix => name.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase)
			));

		await Assert.That(value: violations).IsEmpty()
			.Because(message: String.Join(separator: ", ", values: violations));
	}
}
