using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class TransactionEntityConfiguration : IEntityTypeConfiguration<TransactionEntity>
{
	public void Configure(EntityTypeBuilder<TransactionEntity> builder)
	{
		builder.ToTable(name: "rm_transactions");

		builder.HasKey(keyExpression: t => t.Id);

		builder.Property(propertyExpression: t => t.AccountId)
			.HasColumnName(name: "account_id");

		builder.Property(propertyExpression: t => t.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: t => t.CategoryId)
			.HasColumnName(name: "category_id");

		builder.Property(propertyExpression: t => t.Amount)
			.HasColumnName(name: "amount")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: t => t.Direction)
			.HasColumnName(name: "direction_type")
			.HasMaxLength(maxLength: 10)
			.HasConversion(
				convertToProviderExpression: type => type.ToString().ToLower(),
				convertFromProviderExpression: value => Enum.Parse<DirectionType>(value: value, ignoreCase: true)
			);

		builder.Property(propertyExpression: t => t.ExchangeRate)
			.HasColumnName(name: "exchange_rate")
			.HasColumnType(typeName: "numeric(18,6)");

		builder.Property(propertyExpression: t => t.IsExcluded)
			.HasColumnName(name: "is_excluded");

		builder.Property(propertyExpression: t => t.Description)
			.HasColumnName(name: "description")
			.HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: t => t.IsRatePending)
			.HasColumnName(name: "is_rate_pending");
		
		builder.Property(propertyExpression: t => t.OccurredAt)
			.HasColumnName(name: "occurred_at");

		builder.HasOne<AccountEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.AccountId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<CategoryEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.CategoryId)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);
	}
}