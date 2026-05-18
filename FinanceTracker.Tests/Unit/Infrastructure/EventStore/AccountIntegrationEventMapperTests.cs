using FinanceTracker.Contracts.Events.Account;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.EventStore.EventMapper;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Infrastructure.EventStore;

public sealed class AccountIntegrationEventMapperTests
{
	private sealed record UnknownTestEvent(Guid Id, DateTime OccurredAt) : IEvent;
	
	private CapturingLogger<AccountIntegrationEventMapper> _logger = null!;
	private AccountIntegrationEventMapper _mapper = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_logger = new CapturingLogger<AccountIntegrationEventMapper>();
		_mapper = new AccountIntegrationEventMapper(logger: _logger);
	}

	[Test]
	public async Task Map_AccountCreated_ReturnsAccountCreatedEvent()
	{
		AccountCreated @event = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: Guid.CreateVersion7(),
			Name: Name.Reconstitute(value: "Карта"),
			Type: AccountType.Checking,
			Currency: Currency.Reconstitute(value: "RUB"),
			Balance: 1000m,
			OccurredAt: DateTime.UtcNow
		);

		IAccountIntegrationEvent? result = _mapper.Map(domainEvent: @event);

		await Assert.That(value: result).IsTypeOf<AccountCreatedEvent>();
	}

	[Test]
	public async Task Map_AccountDebited_ReturnsAccountDebitedEvent()
	{
		AccountDebited @event = new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			OccurredAt: DateTime.UtcNow
		);

		IAccountIntegrationEvent? result = _mapper.Map(domainEvent: @event);

		await Assert.That(value: result).IsTypeOf<AccountDebitedEvent>();
	}

	[Test]
	public async Task Map_AccountCredited_ReturnsAccountCreditedEvent()
	{
		AccountCredited @event = new AccountCredited(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 300m,
			ExchangeRate: 1m,
			Description: null,
			OccurredAt: DateTime.UtcNow
		);

		IAccountIntegrationEvent? result = _mapper.Map(domainEvent: @event);

		await Assert.That(value: result).IsTypeOf<AccountCreditedEvent>();
	}

	[Test]
	public async Task Map_UnknownEvent_ReturnsNull()
	{
		UnknownTestEvent @event = new UnknownTestEvent(
			Id: Guid.CreateVersion7(),
			OccurredAt: DateTime.UtcNow
		);

		IAccountIntegrationEvent? result = _mapper.Map(domainEvent: @event);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task Map_UnknownEvent_LogsWarning()
	{
		_mapper.Map(domainEvent: new UnknownTestEvent(
			Id: Guid.CreateVersion7(),
			OccurredAt: DateTime.UtcNow
		));

		await Assert.That(value: _logger.WarningLogged).IsTrue();
	}

	[Test]
	public async Task Map_KnownEvent_DoesNotLogWarning()
	{
		_mapper.Map(domainEvent: new AccountRenamed(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			NewName: Name.Reconstitute(value: "Новое имя"),
			OccurredAt: DateTime.UtcNow
		));

		await Assert.That(value: _logger.WarningLogged).IsFalse();
	}
}