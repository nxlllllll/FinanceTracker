using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database.Context.Account;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;

public sealed class RecurringTransactionEntityConfiguration
	: IEntityTypeConfiguration<RecurringTransactionEntity>
{
	public void Configure(EntityTypeBuilder<RecurringTransactionEntity> builder)
	{
		builder.ToTable(name: "recurring_transactions");

		builder.HasKey(keyExpression: r => r.Id);

		builder.Property(propertyExpression: a => a.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: r => r.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: r => r.AccountId)
			.HasColumnName(name: "account_id");

		builder.Property(propertyExpression: r => r.CategoryId)
			.HasColumnName(name: "category_id");

		builder.Property(propertyExpression: r => r.Amount)
			.HasColumnName(name: "amount")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: r => r.Currency)
			.HasColumnName(name: "currency_code")
			.HasMaxLength(maxLength: 3)
			.HasConversion(
				convertToProviderExpression: currency => currency.Value,
				convertFromProviderExpression: currency => Core.ValueObjects.Currency.Reconstitute(value: currency)
			);

		builder.Property(propertyExpression: r => r.Direction)
			.HasColumnName(name: "direction_type")
			.HasConversion(
				convertToProviderExpression: v => v.ToString().ToLowerInvariant(),
				convertFromProviderExpression: v => Enum.Parse<DirectionType>(value: v, ignoreCase: true)
			)
			.HasMaxLength(maxLength: 10);

		builder.Property(propertyExpression: r => r.DayOfMonth)
			.HasColumnName(name: "day_of_month");

		builder.Property(propertyExpression: r => r.Description)
			.HasColumnName(name: "description")
			.HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: r => r.IsActive)
			.HasColumnName(name: "is_active");

		builder.Property(propertyExpression: r => r.LastExecutedAt)
			.HasColumnName(name: "last_executed_at");

		builder.Property(propertyExpression: r => r.LastMissedAt)
			.HasColumnName(name: "last_missed_at");

		builder.Property(propertyExpression: r => r.RowVersion)
			.HasColumnName(name: "row_version")
			.HasDefaultValue(value: 0);

		builder.Property(propertyExpression: r => r.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: r => r.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<AccountEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: r => r.AccountId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<CategoryEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: r => r.CategoryId)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.Currency)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}
