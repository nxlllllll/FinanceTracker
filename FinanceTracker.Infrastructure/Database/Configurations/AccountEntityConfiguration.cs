using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class AccountEntityConfiguration : IEntityTypeConfiguration<AccountEntity>
{
	public void Configure(EntityTypeBuilder<AccountEntity> builder)
	{
		builder.ToTable("accounts");

		builder.HasKey(a => a.Id);

		builder.Property(a => a.UserId)
			.HasColumnName("user_id");

		builder.Property(a => a.Name)
			.HasColumnName("name")
			.HasMaxLength(100);

		builder.Property(a => a.AccountType)
			.HasColumnName("account_type_code")
			.HasMaxLength(20);

		builder.Property(a => a.Currency)
			.HasColumnName("currency_code")
			.HasMaxLength(3);

		builder.Property(a => a.IsArchived)
			.HasColumnName("is_archived");

		builder.Property(a => a.CreatedAt)
			.HasColumnName("created_at");

		builder.HasOne<AccountBalanceEntity>().WithOne()
			.HasForeignKey<AccountBalanceEntity>(b => b.AccountId);
	}
}