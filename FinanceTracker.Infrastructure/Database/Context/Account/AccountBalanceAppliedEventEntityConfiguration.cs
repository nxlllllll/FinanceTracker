using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Account;

public sealed class AccountBalanceAppliedEventEntityConfiguration : IEntityTypeConfiguration<AccountBalanceAppliedEventEntity>
{
	public void Configure(EntityTypeBuilder<AccountBalanceAppliedEventEntity> builder)
	{
		builder.ToTable(name: "rm_account_balance_applied_events");

		builder.HasKey(keyExpression: e => new { e.AccountId, e.Version });

		builder.Property(propertyExpression: e => e.AccountId)
			.HasColumnName(name: "account_id");

		builder.Property(propertyExpression: e => e.Version)
			.HasColumnName(name: "version");

		builder.Property(propertyExpression: e => e.AppliedAt)
			.HasColumnName(name: "applied_at");
	}
}
