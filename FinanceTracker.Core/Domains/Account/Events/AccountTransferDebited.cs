using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Account.Events;

/// <summary>
/// Raised when funds are debited from an account as part of a transfer to another account.
/// </summary>
/// <param name="Amount">
/// The debited amount in the source account's currency. Balance is reduced by this exact value.
/// </param>
/// <param name="ForexRate">
/// The forex rate at the time of the transfer, stored as metadata for audit purposes.
/// Always 1 for same-currency transfers. Not used in balance calculation — the debit is always
/// applied as <c>Amount</c> directly in the account's own currency.
/// </param>
[EventType(name: "account.transfer_debited")]
public sealed record AccountTransferDebited(
	Guid Id,
	Guid AccountId,
	Guid TransferId,
	Guid ToAccountId,
	decimal Amount,
	decimal ForexRate,
	string? Description,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}
