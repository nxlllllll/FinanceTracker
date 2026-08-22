using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Account;

public sealed class AccountBalanceEntityConfiguration : IEntityTypeConfiguration<AccountBalanceEntity>
{
	public void Configure(EntityTypeBuilder<AccountBalanceEntity> builder)
	{
		builder.ToTable(name: "rm_account_balances");

		builder.HasKey(keyExpression: b => b.AccountId);

		builder.Property(propertyExpression: b => b.Balance).HasColumnType(typeName: "numeric(18,2)");
	}
}
