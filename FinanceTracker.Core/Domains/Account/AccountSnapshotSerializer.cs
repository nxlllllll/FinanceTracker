using System.Text.Json.Serialization;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Account;

/// <summary>
/// Serializes and deserializes an <see cref="Account"/> aggregate to and from a JSON snapshot.
/// Used by the event store to persist periodic state checkpoints, reducing replay time on load.
/// </summary>
public sealed class AccountSnapshotSerializer : ISnapshotSerializer<Account>
{
	private sealed record AccountSnapshotState(
		[property: JsonPropertyName("id")] Guid Id,
		[property: JsonPropertyName("user_id")] Guid UserId,
		[property: JsonPropertyName("name")] Name Name,
		[property: JsonPropertyName("type")] AccountType Type,
		[property: JsonPropertyName("balance")] Money Balance,
		[property: JsonPropertyName("is_archived")] bool IsArchived,
		[property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
		[property: JsonPropertyName("version")] int Version
	);

	public string Serialize(Account aggregate)
	{
		return System.Text.Json.JsonSerializer.Serialize(value: new AccountSnapshotState(
			Id: aggregate.Id,
			UserId: aggregate.UserId,
			Name: aggregate.Name,
			Type: aggregate.Type,
			Balance: aggregate.Balance,
			IsArchived: aggregate.IsArchived,
			CreatedAt: aggregate.CreatedAt,
			Version: aggregate.Version
		), options: FinanceTrackerJsonOptions.Payload);
	}

	public Account Deserialize(SnapshotData snapshot)
	{
		AccountSnapshotState state = System.Text.Json.JsonSerializer.Deserialize<AccountSnapshotState>(
			json: snapshot.State,
			options: FinanceTrackerJsonOptions.Payload
		)!;

		Account account = Account.Reconstitute(
			id: state.Id,
			userId: state.UserId,
			name: state.Name,
			type: state.Type,
			balance: state.Balance,
			isArchived: state.IsArchived,
			createdAt: state.CreatedAt,
			version: state.Version
		);

		return account;
	}
}
