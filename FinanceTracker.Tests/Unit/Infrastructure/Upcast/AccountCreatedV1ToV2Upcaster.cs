using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

namespace FinanceTracker.Tests.Unit.Infrastructure.Upcast;

[UpcasterVersion(from: 1, to: 2)]
public sealed class AccountCreatedV1ToV2Upcaster : EventUpcaster<AccountCreatedV1, AccountCreatedV2>
{
	public override AccountCreatedV2 Upcast(AccountCreatedV1 source) => new AccountCreatedV2(
		Id: source.Id,
		AccountId: source.AccountId,
		UserId: source.UserId,
		Name: source.Name,
		Currency: source.Currency,
		IsArchived: false,
		Version: source.Version,
		OccurredAt: source.OccurredAt
	);
}
