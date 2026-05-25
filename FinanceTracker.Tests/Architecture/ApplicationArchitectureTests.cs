using System.Reflection;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace FinanceTracker.Tests.Architecture;

public sealed class ApplicationArchitectureTests
{
	private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

	[Test]
	public async Task AllIRequestHandlerClasses_ShouldHaveHandlerSuffix()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().ImplementInterface(interfaceType: typeof(IRequestHandler<,>))
			.And().DoNotHaveNameStartingWith(start: "AuthorizedHandlerAdapter")
			.Should().HaveNameEndingWith(end: "Handler")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllIRequestHandlerClasses_ShouldBeSealed()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().ImplementInterface(interfaceType: typeof(IRequestHandler<,>))
			.And().AreClasses()
			.And().DoNotHaveNameStartingWith(start: "AuthorizedHandlerAdapter")
			.Should().BeSealed()
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllIValidatorClasses_ShouldHaveValidatorSuffix()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().ImplementInterface(interfaceType: typeof(FluentValidation.IValidator<>))
			.Should().HaveNameEndingWith(end: "Validator")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllIValidatorClasses_ShouldBeSealed()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().ImplementInterface(interfaceType: typeof(FluentValidation.IValidator<>))
			.And().AreClasses()
			.Should().BeSealed()
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllHandlersAndLoaders_ShouldResideInUseCasesNamespace()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().HaveNameEndingWith(end: "Handler")
			.Or().HaveNameEndingWith(end: "Loader")
			.Should().ResideInNamespaceStartingWith(name: "FinanceTracker.Application.UseCases")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllCommandsAndQueries_ShouldResideInUseCasesNamespace()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().AreNotInterfaces()
			.And().HaveNameEndingWith(end: "Command")
			.Or().HaveNameEndingWith(end: "Query")
			.Should().ResideInNamespaceStartingWith(name: "FinanceTracker.Application.UseCases")
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllIAuthorizedHandlers_ShouldHaveRegisteredRequestHandler()
	{
		IServiceCollection services = new ServiceCollection();
		services.AddApplication();

		Type authorizedHandlerOpen = typeof(IAuthorizedHandler<,,,>);
		Type requestHandlerOpen = typeof(IRequestHandler<,>);
		Type resultOpen = typeof(Result<,>);

		List<string> missing = ApplicationAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == authorizedHandlerOpen))
			.SelectMany(selector: impl => impl.GetInterfaces()
				.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == authorizedHandlerOpen)
				.Select(selector: handlerInterface =>
				{
					Type[] args = handlerInterface.GetGenericArguments();
					Type requestHandlerInterface = requestHandlerOpen.MakeGenericType(args[0], resultOpen.MakeGenericType(args[2], args[3]));
					if (services.Any(predicate: sd => sd.ServiceType == requestHandlerInterface))
						return null;
					return $"{impl.Name} > {requestHandlerInterface.Name}";
				}))
			.Where(predicate: x => x is not null)
			.ToList()!;

		await Assert.That(value: missing).IsEmpty()
			.Because(message: $"Missing IRequestHandler registrations for: {String.Join(separator: ", ", values: missing)}");
	}

	[Test]
	public async Task AllIEntityLoaders_ShouldBeRegisteredForAllTheirInterfaces()
	{
		IServiceCollection services = new ServiceCollection();
		services.AddApplication();

		Type entityLoaderOpen = typeof(IEntityLoader<,,>);

		List<string> missing = ApplicationAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == entityLoaderOpen))
			.SelectMany(selector: impl => impl.GetInterfaces()
				.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == entityLoaderOpen)
				.Select(selector: loaderInterface => services.Any(predicate: sd => sd.ServiceType == loaderInterface)
					? null
					: $"{impl.Name} as {loaderInterface.GetGenericArguments()[0].Name}"
				)
			)
			.Where(predicate: x => x is not null)
			.ToList()!;

		await Assert.That(value: missing).IsEmpty()
			.Because(message: $"Missing IEntityLoader registrations: {String.Join(separator: ", ", values: missing)}");
	}
}
