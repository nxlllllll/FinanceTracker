using System.Reflection;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Persistence;
using MediatR;

namespace FinanceTracker.Tests.Architecture;

public sealed class WriteHandlerArchitectureTests
{
	private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;
	private static readonly Assembly CoreAssembly = typeof(IUnitOfWork).Assembly;

	private static bool PersistsAnAggregate(Type repositoryInterface)
	{
		if (!repositoryInterface.IsInterface || !repositoryInterface.Name.EndsWith(value: "Repository", comparisonType: StringComparison.Ordinal))
			return false;

		return repositoryInterface.GetMethods().Any(
			predicate: m => m.GetParameters().Any(predicate: p => typeof(AggregateRoot).IsAssignableFrom(c: p.ParameterType))
		);
	}

	private static IEnumerable<Type> AllHandlerTypes()
	{
		Type requestHandlerOpen = typeof(IRequestHandler<,>);
		Type authorizedHandlerOpen4 = typeof(IAuthorizedHandler<,,,>);
		Type authorizedHandlerOpen3 = typeof(IAuthorizedHandler<,,>);

		return ApplicationAssembly.GetTypes().Where(predicate: t =>
			t is { IsClass: true, IsAbstract: false } &&
			!t.Name.StartsWith(value: "AuthorizedHandlerAdapter", comparisonType: StringComparison.Ordinal) &&
			t.GetInterfaces().Any(predicate: i => i.IsGenericType && (
				i.GetGenericTypeDefinition() == requestHandlerOpen ||
				i.GetGenericTypeDefinition() == authorizedHandlerOpen4 ||
				i.GetGenericTypeDefinition() == authorizedHandlerOpen3
			))
		);
	}

	[Test]
	public async Task HandlersThatPersistAnAggregate_ShouldDependOnIUnitOfWork()
	{
		List<string> violations = [];

		foreach (Type handlerType in AllHandlerTypes())
		{
			ConstructorInfo? constructor = handlerType.GetConstructors().FirstOrDefault();
			if (constructor is null)
				continue;

			Type[] dependencies = constructor.GetParameters().Select(selector: p => p.ParameterType).ToArray();

			List<string> aggregateRepositories = dependencies.Where(predicate: PersistsAnAggregate).Select(selector: t => t.Name).ToList();

			if (aggregateRepositories.Count == 0)
				continue;

			if (!dependencies.Contains(typeof(IUnitOfWork)))
			{
				violations.Add(item: $"{handlerType.Name} depends on {String.Join(separator: ", ", values: aggregateRepositories)} " +
					"(persists an event-sourced aggregate) but not on IUnitOfWork — a version conflict at " +
					"SaveChangesAsync would surface outside ConcurrencyRetryBehaviour's retry scope and never " +
					"actually be retried.");
			}
		}

		await Assert.That(value: violations).IsEmpty()
			.Because(message: String.Join(separator: "\n", values: violations));
	}

	[Test]
	public async Task PersistsAnAggregate_ShouldStillRecognizeIAccountRepository()
	{
		Type? accountRepository = CoreAssembly.GetTypes().FirstOrDefault(predicate: t => t.IsInterface && t.Name == "IAccountRepository");

		await Assert.That(value: accountRepository).IsNotNull()
			.Because(message: "IAccountRepository was not found in FinanceTracker.Core — did it move or get renamed?");

		await Assert.That(value: PersistsAnAggregate(repositoryInterface: accountRepository!)).IsTrue()
			.Because(message: "IAccountRepository.SaveAsync(Account, ...) takes an AggregateRoot-derived parameter and must be recognized as aggregate persistence.");
	}

	[Test]
	public async Task PersistsAnAggregate_ShouldNotFlagReadModelWriteRepositories()
	{
		Type[] readModelWriteRepositories = CoreAssembly.GetTypes().Where(predicate: t => t is
		{
			IsInterface: true,
			Name: "ITransactionWriteRepository" or "ITransferWriteRepository" or "IBudgetWriteRepository" or "ICategoryWriteRepository"
		}).ToArray();

		await Assert.That(value: readModelWriteRepositories).IsNotEmpty()
			.Because(message: "None of the expected read-model write repositories were found in FinanceTracker.Core — did they move or get renamed?");

		foreach (Type type in readModelWriteRepositories)
		{
			await Assert.That(value: PersistsAnAggregate(repositoryInterface: type)).IsFalse()
				.Because(message: $"{type.Name} mutates via ExecuteUpdateAsync, not the event store — it must not require IUnitOfWork.");
		}
	}
}
