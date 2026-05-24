using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Domains.User.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.User;

public sealed class User : IHasDomainEvents
{
	private readonly List<IDomainEvent> _domainEvents = [];

	public Guid Id { get; private set; }
	public Email Email { get; private set; }
	public string PasswordHash { get; private set; } = String.Empty;
	public Currency BaseCurrency { get; private set; }
	public DateTime CreatedAt { get; private set; }

	public string AggregateType => AggregateTypeNames.User;
	public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
	public void ClearDomainEvents() => _domainEvents.Clear();

	private User() { }

	private void RaiseDomainEvent(IDomainEvent @event)
		=> _domainEvents.Add(item: @event);

	public static Result<User, DomainException> Register(
		DateTime createdAt,
		Email email,
		string passwordHash,
		Currency baseCurrency)
	{
		if (String.IsNullOrWhiteSpace(value: passwordHash))
			return Result<User, DomainException>.Failure(error: new PasswordException(message: "The password hash cannot be empty."));

		User user = new User()
		{
			Id = Guid.CreateVersion7(),
			Email = email,
			PasswordHash = passwordHash,
			BaseCurrency = baseCurrency,
			CreatedAt = createdAt
		};

		user.RaiseDomainEvent(@event: new UserRegistered(
			Id: Guid.CreateVersion7(),
			AggregateId: user.Id,
			Email: email,
			BaseCurrency: baseCurrency,
			OccurredAt: createdAt
		));

		return Result<User, DomainException>.Success(value: user);
	}

	public static User Reconstitute(
		Guid id,
		Email email,
		string passwordHash,
		Currency baseCurrencyCode,
		DateTime createdAt)
	{
		return new User()
		{
			Id = id,
			Email = email,
			PasswordHash = passwordHash,
			BaseCurrency = baseCurrencyCode,
			CreatedAt = createdAt
		};
	}

	public Result<Unit, DomainException> ChangeEmail(Email newEmail, DateTime occurredAt)
	{
		if (Email == newEmail)
			return Result<Unit, DomainException>.Success(value: Unit.Default);

		Email oldEmail = Email;
		Email = newEmail;

		RaiseDomainEvent(@event: new UserEmailChanged(
			Id: Guid.CreateVersion7(),
			AggregateId: Id,
			OldEmail: oldEmail,
			NewEmail: newEmail,
			OccurredAt: occurredAt
		));

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> ChangePassword(string newPasswordHash, DateTime occurredAt)
	{
		if (String.IsNullOrWhiteSpace(value: newPasswordHash))
			return Result<Unit, DomainException>.Failure(error: new PasswordException(message: "The password hash cannot be empty."));

		PasswordHash = newPasswordHash;
		
		RaiseDomainEvent(@event: new UserPasswordChanged(
			Id: Guid.CreateVersion7(),
			AggregateId: Id,
			OccurredAt: occurredAt
		));
		
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> ChangeBaseCurrency(Currency newBaseCurrency, DateTime occurredAt)
	{
		if (BaseCurrency == newBaseCurrency)
			return Result<Unit, DomainException>.Success(value: Unit.Default);

		Currency oldBaseCurrency = BaseCurrency;
		BaseCurrency = newBaseCurrency;

		RaiseDomainEvent(@event: new UserBaseCurrencyChanged(
			Id: Guid.CreateVersion7(),
			AggregateId: Id,
			OldBaseCurrency: oldBaseCurrency,
			NewBaseCurrency: newBaseCurrency,
			OccurredAt: occurredAt
		));

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}