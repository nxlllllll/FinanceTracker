using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Outbox;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Worker.AccountProjection.Consumer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration.Application.Account;

/// <summary>
/// Flow tests: CreateAccount → Event Store → AccountProjection → read model.
/// They check the full chain through a real MediatR pipeline with all the behaviors.
/// </summary>
public sealed class CreateAccountFlowTests : MediatorFixture
{
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
		=> _userBuilder = new UserBuilder(context: Context);

	private CreateAccountCommand BuildCommand(Guid userId, Guid? idempotencyKey = null)
	{
		return new CreateAccountCommand(
				UserId: userId,
				Name: Name.Create(value: "Основной счёт").Value,
				Type: AccountType.Checking,
				Currency: Currency.Create(value: "RUB").Value,
				InitialBalance: 10_000m
			)
		{ IdempotencyKey = idempotencyKey ?? Guid.CreateVersion7() };
	}

	[Test]
	public async Task CreateAccount_ShouldSucceed()
	{
		Guid userId = await _userBuilder.CreateAsync();
		CreateAccountCommand command = new CreateAccountCommand(
				UserId: userId,
				Name: Name.Create(value: "Счёт").Value,
				Type: AccountType.Checking,
				Currency: Currency.Create(value: "RUB").Value,
				InitialBalance: 0m
			)
		{ IdempotencyKey = Guid.CreateVersion7() };

		Result<Guid, AppException> result = await Mediator.Send(request: command);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task CreateAccount_ShouldPersistEventInEventStore()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Result<Guid, AppException> result = await Mediator.Send(request: BuildCommand(userId: userId));

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool eventExists = await readCtx.Events.AnyAsync(
			predicate: e => e.AggregateId == result.Value! && e.EventType == "account.created"
		);

		await Assert.That(value: eventExists).IsTrue();
	}

	[Test]
	public async Task CreateAccount_ShouldCreateOutboxMessage()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Result<Guid, AppException> result = await Mediator.Send(request: BuildCommand(userId: userId));

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool outboxExists = await readCtx.OutboxMessages.AnyAsync(
			predicate: o => o.AggregateId == result.Value!
		);

		await Assert.That(value: outboxExists).IsTrue();
	}

	[Test]
	public async Task CreateAccount_AfterProjection_ShouldUpdateReadModel()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Result<Guid, AppException> result = await Mediator.Send(request: BuildCommand(userId: userId));
		Guid accountId = result.Value!;

		await ApplyProjectionAsync(accountId: accountId);

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool accountExists = await readCtx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		decimal balance = await readCtx.AccountBalances
			.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstOrDefaultAsync();

		await Assert.That(value: accountExists).IsTrue();
		await Assert.That(value: balance).IsEqualTo(expected: 10_000m);
	}

	[Test]
	public async Task CreateAccount_WithSameIdempotencyKey_ShouldReturnCachedResult()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid idempotencyKey = Guid.CreateVersion7();
		CreateAccountCommand command = BuildCommand(userId: userId, idempotencyKey: idempotencyKey);

		Result<Guid, AppException> first = await Mediator.Send(request: command);
		Result<Guid, AppException> second = await Mediator.Send(request: command);

		await Assert.That(value: first.IsSuccess).IsTrue();
		await Assert.That(value: second.IsSuccess).IsTrue();
		await Assert.That(value: second.Value).IsEqualTo(expected: first.Value);

		await using FinanceTrackerContext readCtx = CreateReadContext();
		int eventCount = await readCtx.Events.CountAsync(
			predicate: e => e.AggregateId == first.Value! && e.EventType == "account.created"
		);
		await Assert.That(value: eventCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task CreateAccount_WithNegativeBalance_ShouldFail()
	{
		Guid userId = await _userBuilder.CreateAsync();
		CreateAccountCommand command = new CreateAccountCommand(
				UserId: userId,
				Name: Name.Create(value: "Счёт").Value,
				Type: AccountType.Checking,
				Currency: Currency.Create(value: "RUB").Value,
				InitialBalance: -100m
			)
		{ IdempotencyKey = Guid.CreateVersion7() };

		Result<Guid, AppException> result = await Mediator.Send(request: command);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ValidationException>();
	}

	[Test]
	public async Task CreateAccount_WithAmountExceedingLimit_ShouldFailWithValidationException()
	{
		Guid userId = await _userBuilder.CreateAsync();
		CreateAccountCommand command = new CreateAccountCommand(
				UserId: userId,
				Name: Name.Create(value: "Счёт").Value,
				Type: AccountType.Checking,
				Currency: Currency.Create(value: "RUB").Value,
				InitialBalance: 1_000_000_000_000m // заведомо больше MoneyLimitsOptions.MaxAmount
			)
		{ IdempotencyKey = Guid.CreateVersion7() };

		Result<Guid, AppException> result = await Mediator.Send(request: command);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ValidationException>();
	}

	/// <summary>
	/// Reproduces what AccountEventsConsumer does:
	/// reads events from the Event Store and applies them to the read model via AccountProjection.
	/// </summary>
	private async Task ApplyProjectionAsync(Guid accountId)
	{
		AccountEventsConsumer consumer = Host.Services.GetRequiredService<AccountEventsConsumer>();
		OutboxMessageEntity outbox = await Context.OutboxMessages
			.Where(predicate: o => o.AggregateId == accountId)
			.FirstAsync();

		OutboxPayload payload = System.Text.Json.JsonSerializer.Deserialize<OutboxPayload>(
			json: outbox.Payload,
			options: Core.Converters.Json.FinanceTrackerJsonOptions.Payload
		)!;

		await consumer.HandleAsync(message: new AccountEventsMessage(
			MessageId: Guid.CreateVersion7(),
			AggregateId: accountId,
			AggregateType: "Account",
			CorrelationId: Guid.CreateVersion7(),
			Events: payload.Events.Select(selector: e => new EventEnvelope(
				EventType: e.EventType,
				EventPayload: e.EventPayload
			)).ToList()
		), ct: CancellationToken.None);
	}
}
