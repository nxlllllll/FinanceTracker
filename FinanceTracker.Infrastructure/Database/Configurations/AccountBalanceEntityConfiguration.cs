using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class AccountBalanceEntityConfiguration : IEntityTypeConfiguration<AccountBalanceEntity>
{
	public void Configure(EntityTypeBuilder<AccountBalanceEntity> builder)
	{
		builder.ToTable(name: "rm_account_balances");

		builder.HasKey(keyExpression: b => b.AccountId);

		builder.Property(propertyExpression: b => b.AccountId)
			.HasColumnName(name: "account_id");

		builder.Property(propertyExpression: b => b.Balance)
			.HasColumnName(name: "balance")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: b => b.LastVersion)
			.HasColumnName(name: "last_version");

		builder.Property(propertyExpression: b => b.UpdatedAt)
			.HasColumnName(name: "updated_at");
	}
}
