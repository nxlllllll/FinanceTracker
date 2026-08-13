using System.Reflection;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Correlation;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Application.Behaviours.Retry;
using FinanceTracker.Application.Behaviours.Tracing;
using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NetArchTest.Rules;
using TUnit.Assertions.Enums;
using TestResult = NetArchTest.Rules.TestResult;

namespace FinanceTracker.Tests.Architecture;

public sealed class ApplicationArchitectureTests
{
	private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

	private static async Task AssertPasses(TestResult result)
	{
		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllIRequestHandlerClasses_ShouldHaveHandlerSuffix()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().ImplementInterface(interfaceType: typeof(IRequestHandler<,>))
			.And().DoNotHaveNameStartingWith(start: "AuthorizedHandlerAdapter")
			.Should().HaveNameEndingWith(end: "Handler")
			.GetResult();

		await AssertPasses(result: result);
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

		await AssertPasses(result: result);
	}

	[Test]
	public async Task AllIValidatorClasses_ShouldHaveValidatorSuffix()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().ImplementInterface(interfaceType: typeof(FluentValidation.IValidator<>))
			.Should().HaveNameEndingWith(end: "Validator")
			.GetResult();

		await AssertPasses(result: result);
	}

	[Test]
	public async Task AllIValidatorClasses_ShouldBeSealed()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().ImplementInterface(interfaceType: typeof(FluentValidation.IValidator<>))
			.And().AreClasses()
			.Should().BeSealed()
			.GetResult();

		await AssertPasses(result: result);
	}

	[Test]
	public async Task AllHandlersAndLoaders_ShouldResideInUseCasesNamespace()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().HaveNameEndingWith(end: "Handler")
			.Or().HaveNameEndingWith(end: "Loader")
			.Should().ResideInNamespaceStartingWith(name: "FinanceTracker.Application.UseCases")
			.GetResult();

		await AssertPasses(result: result);
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

		await AssertPasses(result: result);
	}

	private static List<string> FindMissingAuthorizedHandlerRegistrations(
		IServiceCollection services,
		Type authorizedHandlerOpen,
		Func<Type[], Type> resolveExpectedRequestHandlerInterface)
	{
		return ApplicationAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == authorizedHandlerOpen))
			.SelectMany(selector: impl => impl.GetInterfaces()
				.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == authorizedHandlerOpen)
				.Select(selector: handlerInterface =>
				{
					Type requestHandlerInterface = resolveExpectedRequestHandlerInterface(arg: handlerInterface.GetGenericArguments());
					if (services.Any(predicate: sd => sd.ServiceType == requestHandlerInterface))
						return null;
					return $"{impl.Name} > {requestHandlerInterface.Name}";
				}))
			.Where(predicate: x => x is not null)
			.ToList()!;
	}

	[Test]
	public async Task AllIAuthorizedHandlers_ShouldHaveRegisteredRequestHandler()
	{
		IServiceCollection services = new ServiceCollection();
		services.AddApplication();

		Type requestHandlerOpen = typeof(IRequestHandler<,>);
		Type resultOpen = typeof(Result<,>);

		List<string> missing = FindMissingAuthorizedHandlerRegistrations(
			services: services,
			authorizedHandlerOpen: typeof(IAuthorizedHandler<,,,>),
			resolveExpectedRequestHandlerInterface: args => requestHandlerOpen.MakeGenericType(args[0], resultOpen.MakeGenericType(args[2], args[3]))
		);

		missing.AddRange(collection: FindMissingAuthorizedHandlerRegistrations(
			services: services,
			authorizedHandlerOpen: typeof(IAuthorizedHandler<,,>),
			resolveExpectedRequestHandlerInterface: args => requestHandlerOpen.MakeGenericType(args[0], resultOpen.MakeGenericType(args[1], args[2]))
		));

		await Assert.That(value: missing).IsEmpty()
			.Because(message: $"Missing IRequestHandler registrations for: {String.Join(separator: ", ", values: missing)}");
	}

	private static List<string> FindMissingLoaderRegistrations(IServiceCollection services, Type loaderOpen)
	{
		return ApplicationAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == loaderOpen))
			.SelectMany(selector: impl => impl.GetInterfaces()
				.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == loaderOpen)
				.Select(selector: loaderInterface => services.Any(predicate: sd => sd.ServiceType == loaderInterface)
					? null
					: $"{impl.Name} as {loaderInterface.GetGenericArguments()[0].Name}"
				)
			)
			.Where(predicate: x => x is not null)
			.ToList()!;
	}

	[Test]
	public async Task AllIEntityLoaders_ShouldBeRegisteredForAllTheirInterfaces()
	{
		IServiceCollection services = new ServiceCollection();
		services.AddApplication();

		List<string> missing = FindMissingLoaderRegistrations(services: services, loaderOpen: typeof(IEntityLoader<,,>));
		missing.AddRange(collection: FindMissingLoaderRegistrations(services: services, loaderOpen: typeof(IEntityLoader<,>)));

		await Assert.That(value: missing).IsEmpty()
			.Because(message: $"Missing IEntityLoader registrations: {String.Join(separator: ", ", values: missing)}");
	}

	[Test]
	public async Task AllPipelineBehaviours_ShouldBeSealed()
	{
		TestResult result = Types.InAssembly(assembly: ApplicationAssembly)
			.That().ImplementInterface(interfaceType: typeof(IPipelineBehavior<,>))
			.And().AreClasses()
			.Should().BeSealed()
			.GetResult();

		await AssertPasses(result: result);
	}

	private static Type[] FindRequestsNotReturningResult(string nameSuffix)
	{
		Type resultOpenType = typeof(Result<,>);

		return ApplicationAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith(value: nameSuffix, comparisonType: StringComparison.Ordinal))
			.Where(predicate: t =>
			{
				Type? requestInterface = t.GetInterfaces().FirstOrDefault(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

				if (requestInterface is null)
					return false;

				Type responseType = requestInterface.GetGenericArguments()[0];
				return !responseType.IsGenericType || responseType.GetGenericTypeDefinition() != resultOpenType;
			}).ToArray();
	}

	[Test]
	public async Task AllWriteCommands_ShouldReturnResult()
	{
		Type[] violations = FindRequestsNotReturningResult(nameSuffix: "Command");

		await Assert.That(value: violations.Select(t => t.Name)).IsEmpty()
			.Because(message: $"Commands not returning Result<,>: {String.Join(separator: ", ", values: violations.Select(t => t.Name))}");
	}

	[Test]
	public async Task AllQueries_ShouldReturnResult()
	{
		Type[] violations = FindRequestsNotReturningResult(nameSuffix: "Query");

		await Assert.That(value: violations.Select(t => t.Name)).IsEmpty()
			.Because(message: $"Queries not returning Result<,>: {String.Join(separator: ", ", values: violations.Select(t => t.Name))}");
	}

	[Test]
	public async Task AllCommandsAndQueries_ShouldSatisfyEveryRegisteredPipelineBehaviourConstraint()
	{
		IServiceCollection services = new ServiceCollection();
		services.AddApplication();

		Type pipelineBehaviourOpen = typeof(IPipelineBehavior<,>);

		Type[] behaviourOpenTypes = services
			.Where(predicate: sd => sd.ServiceType.IsGenericType && sd.ServiceType.GetGenericTypeDefinition() == pipelineBehaviourOpen && sd.ImplementationType is not null)
			.Select(selector: sd => sd.ImplementationType!)
			.Distinct()
			.ToArray();

		await Assert.That(value: behaviourOpenTypes).IsNotEmpty()
			.Because(message: "No open-generic IPipelineBehavior<,> registrations were discovered — AddApplication()'s registration mechanism may have changed.");

		Type requestOpen = typeof(IRequest<>);

		List<string> violations = [];

		foreach (Type requestType in ApplicationAssembly.GetTypes().Where(predicate: t => t is { IsClass: true, IsAbstract: false }))
		{
			Type? requestInterface = requestType.GetInterfaces().FirstOrDefault(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == requestOpen);

			if (requestInterface is null)
				continue;

			Type responseType = requestInterface.GetGenericArguments()[0];

			foreach (Type behaviourOpenType in behaviourOpenTypes)
			{
				try
				{
					_ = behaviourOpenType.MakeGenericType(requestType, responseType);
				}
				catch (ArgumentException)
				{
					violations.Add(item: $"{requestType.Name} (response: {responseType.Name}) does not satisfy {behaviourOpenType.Name}'s generic constraint");
				}
			}
		}

		await Assert.That(value: violations).IsEmpty()
			.Because(message: String.Join(separator: "\n", values: violations));
	}

	[Test]
	public async Task AllCommandsWithPasswordProperties_ShouldOverrideToStringToRedactThem()
	{
		List<string> offenders = ApplicationAssembly.GetTypes()
			.Where(predicate: t => t.GetProperties().Any(predicate: p =>
				p.Name.Contains(value: "Password", comparisonType: StringComparison.Ordinal) &&
				p.PropertyType == typeof(string)
			))
			.Where(predicate: t => t.GetMethod(name: nameof(ToString), types: Type.EmptyTypes)?.DeclaringType != t)
			.Select(selector: t => t.Name)
			.ToList();

		await Assert.That(value: offenders).IsEmpty().Because(message: $"""
			These types have a string property containing "Password" but don't override ToString()
			to redact it — the compiler-generated record ToString() would print the raw password.
			Offenders: {String.Join(separator: ", ", values: offenders)}
		""");
	}

	[Test]
	public async Task PipelineBehaviours_ShouldBeRegisteredInTheExpectedOrder()
	{
		Type[] expectedOrder =
		[
			typeof(ObservabilityBehaviour<,>),
			typeof(CorrelationBehaviour<,>),
			typeof(AuthRateLimitingBehaviour<,>),
			typeof(RateLimitingBehaviour<,>),
			typeof(ValidationBehaviour<,>),
			typeof(PostCommitNotificationBehaviour<,>),
			typeof(TransientRetryBehaviour<,>),
			typeof(IdempotencyBehaviour<,>),
			typeof(ConcurrencyRetryBehaviour<,>)
		];

		IServiceCollection services = new ServiceCollection();
		services.AddApplication();

		Type pipelineBehaviourOpen = typeof(IPipelineBehavior<,>);

		Type[] actualOrder = services
			.Where(predicate: sd => sd.ServiceType.IsGenericType && sd.ServiceType.GetGenericTypeDefinition() == pipelineBehaviourOpen && sd.ImplementationType is not null)
			.Select(selector: sd => sd.ImplementationType!)
			.ToArray();

		await Assert.That(value: actualOrder).IsEquivalentTo(expectedOrder, CollectionOrdering.Matching).Because(message: $"""
			Expected: {String.Join(separator: " -> ", values: expectedOrder.Select(selector: t => t.Name))}
			Actual: {String.Join(separator: " -> ", values: actualOrder.Select(selector: t => t.Name))}
		""");
	}
}
