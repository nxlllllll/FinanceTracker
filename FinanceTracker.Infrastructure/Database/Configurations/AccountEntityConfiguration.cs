using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class AccountEntityConfiguration : IEntityTypeConfiguration<AccountEntity>
{
	public void Configure(EntityTypeBuilder<AccountEntity> builder)
	{
		builder.ToTable(name: "accounts");

		builder.HasKey(keyExpression: a => a.Id);

		builder.Property(propertyExpression: a => a.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: a => a.Name)
			.HasColumnName(name: "name")
			.HasMaxLength(maxLength: 100);

		builder.Property(propertyExpression: a => a.AccountType)
			.HasColumnName(name: "account_type_code")
			.HasMaxLength(maxLength: 20);

		builder.Property(propertyExpression: a => a.Currency)
			.HasColumnName(name: "currency_code")
			.HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: a => a.IsArchived)
			.HasColumnName(name: "is_archived");

		builder.Property(propertyExpression: a => a.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.HasOne<AccountBalanceEntity>().WithOne()
			.HasForeignKey<AccountBalanceEntity>(foreignKeyExpression: b => b.AccountId);
		
		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: a => a.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: a => a.Currency)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);

		builder.HasOne<AccountTypeEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: a => a.AccountType)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);
	}
}