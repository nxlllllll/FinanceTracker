using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Account;

public sealed class AccountBalanceAppliedEventEntityConfiguration : IEntityTypeConfiguration<AccountBalanceAppliedEventEntity>
{
	public void Configure(EntityTypeBuilder<AccountBalanceAppliedEventEntity> builder)
	{
		builder.ToTable(name: "rm_account_balance_applied_events");

		builder.HasKey(keyExpression: e => new { e.AccountId, e.Version });
	}
}
