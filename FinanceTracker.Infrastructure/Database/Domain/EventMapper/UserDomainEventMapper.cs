using FinanceTracker.Contracts.Events.Domain;
using FinanceTracker.Contracts.Events.User;
using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Domains.User.Events;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.Domain.EventMapper;

public sealed class UserDomainEventMapper(
	ILogger<UserDomainEventMapper> logger
) : IDomainEventMapper
{
	public IDomainIntegrationEvent? Map(IDomainEvent @event) => @event switch
	{
		UserRegistered e => new UserRegisteredEvent(
			EventId: e.Id,
			UserId: e.AggregateId,
			Email: e.Email.Value,
			BaseCurrency: e.BaseCurrency.Value,
			OccurredAt: e.OccurredAt
		),
		UserEmailChanged e => new UserEmailChangedEvent(
			EventId: e.Id,
			UserId: e.AggregateId,
			NewEmail: e.NewEmail.Value,
			OccurredAt: e.OccurredAt
		),
		UserBaseCurrencyChanged e => new UserBaseCurrencyChangedEvent(
			EventId: e.Id,
			UserId: e.AggregateId,
			NewBaseCurrency: e.NewBaseCurrency.Value,
			OccurredAt: e.OccurredAt
		),
		UserPasswordChanged e => new UserPasswordChangedEvent(
			EventId: e.Id,
			UserId: e.AggregateId,
			NewPassword: e.NewPassword,
			OccurredAt: e.OccurredAt
		),
		_ => ExecuteDefaultCase(@event: @event)
	};

	private IDomainIntegrationEvent? ExecuteDefaultCase(IDomainEvent @event)
	{
		logger.ZLogWarning(message: 
			$"[DomainEventMapper] No domain event mapping defined for event '{@event.GetType().Name}'. " +
			$"The event will not be published to the outbox. Add a mapping if outbox propagation is required."
		);
		return null;
	}
}