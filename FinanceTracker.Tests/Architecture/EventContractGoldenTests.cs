using System.Reflection;
using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Architecture;

/// <summary>
/// Golden-file contract tests for every persisted <see cref="IEvent"/>. Each test serializes a
/// representative instance of one event type with the exact <see cref="FinanceTrackerJsonOptions.Payload"/>
/// options used by <c>PostgresEventStore</c>, and compares the result byte-for-byte against a fixed
/// reference string.
/// </summary>
public sealed class EventContractGoldenTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;

	private static readonly Guid Id = Guid.Parse(input: "00000000-0000-0000-0000-000000000001");
	private static readonly Guid AccountId = Guid.Parse(input: "00000000-0000-0000-0000-000000000002");
	private static readonly Guid PermissionUserId = Guid.Parse(input: "00000000-0000-0000-0000-000000000009");
	private static readonly Guid AdminId = Guid.Parse(input: "00000000-0000-0000-0000-000000000010");
	private static readonly Guid RoleId = Guid.Parse(input: "00000000-0000-0000-0000-000000000011");
	private static readonly DateTimeOffset OccurredAt = new DateTimeOffset(year: 2026, month: 1, day: 15, hour: 12, minute: 30, second: 0, offset: TimeSpan.Zero);

	private static string Serialize(IEvent @event) => JsonSerializer.Serialize(
		value: @event,
		inputType: @event.GetType(),
		options: FinanceTrackerJsonOptions.Payload
	);

	private static readonly Dictionary<Type, (IEvent Event, string Golden)> Cases = new Dictionary<Type, (IEvent Event, string Golden)>()
	{
		[typeof(AccountCreated)] = (
			new AccountCreated(
				Id: Id,
				AccountId: AccountId,
				UserId: Guid.Parse(input: "00000000-0000-0000-0000-000000000003"),
				Name: Name.Create(value: "Checking").Value,
				Type: AccountType.Checking,
				Currency: Currency.Create(value: "USD").Value,
				Balance: 1000.50m,
				Version: 1,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","UserId":"00000000-0000-0000-0000-000000000003","Name":"Checking","Type":"Checking","Currency":"USD","Balance":1000.50,"Version":1,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountDebited)] = (
			new AccountDebited(
				Id: Id,
				AccountId: AccountId,
				TransactionId: Guid.Parse(input: "00000000-0000-0000-0000-000000000004"),
				CategoryId: Guid.Parse(input: "00000000-0000-0000-0000-000000000005"),
				Amount: 250.75m,
				ExchangeRate: 1.0m,
				Description: "Groceries",
				Version: 2,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","TransactionId":"00000000-0000-0000-0000-000000000004","CategoryId":"00000000-0000-0000-0000-000000000005","Amount":250.75,"ExchangeRate":1.0,"Description":"Groceries","Version":2,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountCredited)] = (
			new AccountCredited(
				Id: Id,
				AccountId: AccountId,
				TransactionId: Guid.Parse(input: "00000000-0000-0000-0000-000000000004"),
				CategoryId: Guid.Parse(input: "00000000-0000-0000-0000-000000000005"),
				Amount: 500.00m,
				ExchangeRate: 1.0m,
				Description: "Salary",
				Version: 3,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","TransactionId":"00000000-0000-0000-0000-000000000004","CategoryId":"00000000-0000-0000-0000-000000000005","Amount":500.00,"ExchangeRate":1.0,"Description":"Salary","Version":3,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountArchived)] = (
			new AccountArchived(
				Id: Id,
				AccountId: AccountId,
				Version: 4,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","Version":4,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountUnarchived)] = (
			new AccountUnarchived(
				Id: Id,
				AccountId: AccountId,
				Version: 5,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","Version":5,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountRenamed)] = (
			new AccountRenamed(
				Id: Id,
				AccountId: AccountId,
				NewName: Name.Create(value: "Savings").Value,
				Version: 6,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","NewName":"Savings","Version":6,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountTransferDebited)] = (
			new AccountTransferDebited(
				Id: Id,
				AccountId: AccountId,
				TransferId: Guid.Parse(input: "00000000-0000-0000-0000-000000000006"),
				ToAccountId: Guid.Parse(input: "00000000-0000-0000-0000-000000000007"),
				Amount: 100.00m,
				ForexRate: 1.0m,
				Description: "Transfer out",
				Version: 7,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","TransferId":"00000000-0000-0000-0000-000000000006","ToAccountId":"00000000-0000-0000-0000-000000000007","Amount":100.00,"ForexRate":1.0,"Description":"Transfer out","Version":7,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountTransferCredited)] = (
			new AccountTransferCredited(
				Id: Id,
				AccountId: AccountId,
				TransferId: Guid.Parse(input: "00000000-0000-0000-0000-000000000006"),
				FromAccountId: Guid.Parse(input: "00000000-0000-0000-0000-000000000007"),
				Amount: 100.00m,
				ExchangeRate: 0.92m,
				Description: "Transfer in",
				Version: 8,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","TransferId":"00000000-0000-0000-0000-000000000006","FromAccountId":"00000000-0000-0000-0000-000000000007","Amount":100.00,"ExchangeRate":0.92,"Description":"Transfer in","Version":8,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountTransactionReverted)] = (
			new AccountTransactionReverted(
				Id: Id,
				AccountId: AccountId,
				TransactionId: Guid.Parse(input: "00000000-0000-0000-0000-000000000004"),
				CategoryId: Guid.Parse(input: "00000000-0000-0000-0000-000000000005"),
				Amount: 250.75m,
				ExchangeRate: 1.0m,
				Direction: DirectionType.Debit,
				Description: "Groceries",
				Version: 4,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","TransactionId":"00000000-0000-0000-0000-000000000004","CategoryId":"00000000-0000-0000-0000-000000000005","Amount":250.75,"ExchangeRate":1.0,"Direction":"Debit","Description":"Groceries","Version":4,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountTransferRefunded)] = (
			new AccountTransferRefunded(
				Id: Id,
				AccountId: AccountId,
				TransferId: Guid.Parse(input: "00000000-0000-0000-0000-000000000006"),
				Amount: 100.00m,
				Description: "Refund",
				Version: 9,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","TransferId":"00000000-0000-0000-0000-000000000006","Amount":100.00,"Description":"Refund","Version":9,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(AccountBalanceAdjusted)] = (
			new AccountBalanceAdjusted(
				Id: Id,
				AccountId: AccountId,
				SourceId: Guid.Parse(input: "00000000-0000-0000-0000-000000000008"),
				SourceType: AggregateTypeNames.Transaction,
				OldRate: 0.90m,
				NewRate: 0.92m,
				Amount: 100.00m,
				Delta: 2.17m,
				Version: 10,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","AccountId":"00000000-0000-0000-0000-000000000002","SourceId":"00000000-0000-0000-0000-000000000008","SourceType":"Transaction","OldRate":0.90,"NewRate":0.92,"Amount":100.00,"Delta":2.17,"Version":10,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(UserPermissionCreated)] = (
			new UserPermissionCreated(
				Id: Id,
				UserId: PermissionUserId,
				Version: 11,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","UserId":"00000000-0000-0000-0000-000000000009","Version":11,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(PermissionGranted)] = (
			new PermissionGranted(
				Id: Id,
				UserId: PermissionUserId,
				GrantedBy: AdminId,
				Permission: "account:write",
				Version: 12,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","UserId":"00000000-0000-0000-0000-000000000009","GrantedBy":"00000000-0000-0000-0000-000000000010","Permission":"account:write","Version":12,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(PermissionRevoked)] = (
			new PermissionRevoked(
				Id: Id,
				UserId: PermissionUserId,
				RevokedBy: AdminId,
				Permission: "account:write",
				Version: 13,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","UserId":"00000000-0000-0000-0000-000000000009","RevokedBy":"00000000-0000-0000-0000-000000000010","Permission":"account:write","Version":13,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(UserRoleCreated)] = (
			new UserRoleCreated(
				Id: Id,
				UserId: PermissionUserId,
				Version: 14,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","UserId":"00000000-0000-0000-0000-000000000009","Version":14,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(RoleAssigned)] = (
			new RoleAssigned(
				Id: Id,
				UserId: PermissionUserId,
				RoleId: RoleId,
				AssignedBy: AdminId,
				Version: 15,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","UserId":"00000000-0000-0000-0000-000000000009","RoleId":"00000000-0000-0000-0000-000000000011","AssignedBy":"00000000-0000-0000-0000-000000000010","Version":15,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
		[typeof(RoleRemoved)] = (
			new RoleRemoved(
				Id: Id,
				UserId: PermissionUserId,
				RoleId: RoleId,
				RemovedBy: AdminId,
				Version: 16,
				OccurredAt: OccurredAt
			),
			"""{"Id":"00000000-0000-0000-0000-000000000001","UserId":"00000000-0000-0000-0000-000000000009","RoleId":"00000000-0000-0000-0000-000000000011","RemovedBy":"00000000-0000-0000-0000-000000000010","Version":16,"OccurredAt":"2026-01-15T12:30:00+00:00"}"""
		),
	};

	[Test]
	public async Task EventSerialization_ShouldMatchGoldenContract()
	{
		List<string> mismatches = [];

		foreach ((Type eventType, (IEvent @event, string golden)) in Cases)
		{
			string actual = Serialize(@event: @event);

			if (actual != golden) mismatches.Add(item: $"""
			{eventType.Name}:
			expected:
			{golden}
			actual:
			{actual}
			""");
		}

		await Assert.That(value: mismatches).IsEmpty().Because(message: $"""
		One or more events' serialized shape changed:
		{String.Join(separator: "\n", values: mismatches)}
		If this is an intentional new event version, add an upcaster for the old shape and update the golden value deliberately — do not just copy the new output in.
		""");
	}

	/// <summary>
	/// Ensures every concrete <see cref="IEvent"/> in the assembly has a golden case above — so a newly
	/// added event type is caught immediately instead of silently shipping without contract coverage.
	/// </summary>
	[Test]
	public async Task AllIEventTypes_ShouldHaveAGoldenCase()
	{
		Type[] allEventTypes = CoreAssembly.GetTypes().Where(predicate: t => t is { IsClass: true, IsAbstract: false } && typeof(IEvent).IsAssignableFrom(c: t)).ToArray();
		Type[] missing = allEventTypes.Where(predicate: t => !Cases.ContainsKey(key: t)).ToArray();

		await Assert.That(value: missing.Select(t => t.Name)).IsEmpty()
			.Because(message: $"Event types missing a golden contract case: {String.Join(separator: ", ", values: missing.Select(t => t.Name))}");
	}
}
