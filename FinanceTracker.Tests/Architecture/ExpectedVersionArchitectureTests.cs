using System.Reflection;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;

namespace FinanceTracker.Tests.Architecture;

/// <summary>
/// Guards the wiring that <see cref="AuthorizedHandlerAdapter{TRequest,TEntity,TValue,TError}"/>
/// cannot express as a generic constraint.
/// </summary>
public sealed class ExpectedVersionArchitectureTests
{
	private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

	private static IEnumerable<Type> ClosedInterfacesOf(Type openGeneric)
	{
		return ApplicationAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false })
			.SelectMany(selector: t => t.GetInterfaces())
			.Where(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric)
			.Distinct();
	}

	private static bool SendsAnExpectedVersion(Type requestType)
		=> typeof(IHasExpectedVersion).IsAssignableFrom(c: requestType);

	[Test]
	public async Task RequestsCarryingAnExpectedVersion_ShouldLoadAVersionedEntity()
	{
		List<string> violations = [];

		foreach (Type handlerInterface in ClosedInterfacesOf(openGeneric: typeof(IAuthorizedHandler<,,,>)))
		{
			Type[] arguments = handlerInterface.GetGenericArguments();
			Type requestType = arguments[0];
			Type entityType = arguments[1];

			if (!SendsAnExpectedVersion(requestType: requestType))
				continue;

			if (!typeof(IHasVersion).IsAssignableFrom(c: entityType))
				violations.Add(item: $"{requestType.Name} loads {entityType.Name}");
		}

		await Assert.That(value: violations).IsEmpty().Because(message:
			$"These requests promise optimistic concurrency but load an entity with no version, so the " +
			$"precondition cannot be checked and the client would be told its If-Match was honoured when it " +
			$"was not. Load a versioned entity or drop {nameof(IHasExpectedVersion)}: " +
			$"{String.Join(separator: ", ", values: violations)}"
		);
	}

	[Test]
	public async Task RequestsCarryingAnExpectedVersion_ShouldUseAnErrorTypeThatFitsPreconditionFailed()
	{
		List<string> violations = [];

		foreach (Type handlerInterface in ClosedInterfacesOf(openGeneric: typeof(IAuthorizedHandler<,,,>)))
		{
			Type[] arguments = handlerInterface.GetGenericArguments();
			Type requestType = arguments[0];
			Type errorType = arguments[3];

			if (!SendsAnExpectedVersion(requestType: requestType))
				continue;

			if (!errorType.IsAssignableFrom(c: typeof(PreconditionFailedException)))
				violations.Add(item: $"{requestType.Name} uses {errorType.Name}");
		}

		await Assert.That(value: violations).IsEmpty().Because(message:
			$"A version mismatch is reported as {nameof(PreconditionFailedException)}, which these error types " +
			$"are too narrow to hold — the adapter would throw while converting it. Widen TError to " +
			$"AppException or drop {nameof(IHasExpectedVersion)}: " +
			$"{String.Join(separator: ", ", values: violations)}"
		);
	}

	[Test]
	public async Task RequestsCarryingAnExpectedVersion_ShouldNotUseTheEntitylessHandler()
	{
		List<string> violations = [];

		foreach (Type handlerInterface in ClosedInterfacesOf(openGeneric: typeof(IAuthorizedHandler<,,>)))
		{
			Type requestType = handlerInterface.GetGenericArguments()[0];

			if (SendsAnExpectedVersion(requestType: requestType))
				violations.Add(item: requestType.Name);
		}

		await Assert.That(value: violations).IsEmpty().Because(message:
			$"The entityless handler never loads anything to compare a version against, so an expected " +
			$"version on these requests is silently ignored. Move them to the four-argument " +
			$"{nameof(IAuthorizedHandler<,,,>)} or drop " +
			$"{nameof(IHasExpectedVersion)}: {String.Join(separator: ", ", values: violations)}"
		);
	}
}
