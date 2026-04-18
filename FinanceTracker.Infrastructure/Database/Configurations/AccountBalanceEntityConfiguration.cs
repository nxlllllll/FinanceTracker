using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class AccountBalanceEntityConfiguration : IEntityTypeConfiguration<AccountBalanceEntity>
{
	public void Configure(EntityTypeBuilder<AccountBalanceEntity> builder)
	{
		builder.ToTable("rm_account_balances");

		builder.HasKey(b => b.AccountId);

		builder.Property(b => b.AccountId)
			.HasColumnName("account_id");

		builder.Property(b => b.Balance)
			.HasColumnName("balance")
			.HasColumnType("numeric(18,2)");

		builder.Property(b => b.LastVersion)
			.HasColumnName("last_version");

		builder.Property(b => b.UpdatedAt)
			.HasColumnName("updated_at");
	}
}