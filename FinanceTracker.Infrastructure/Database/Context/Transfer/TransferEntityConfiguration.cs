using FinanceTracker.Infrastructure.Database.Context.Account;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Context.User;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Transfer;

public sealed class TransferEntityConfiguration : IEntityTypeConfiguration<TransferEntity>
{
	public void Configure(EntityTypeBuilder<TransferEntity> builder)
	{
		builder.ToTable(name: "rm_transfers");

		builder.HasKey(keyExpression: t => t.Id);

		builder.Property(propertyExpression: a => a.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: t => t.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: t => t.FromAccountId)
			.HasColumnName(name: "from_account_id");

		builder.Property(propertyExpression: t => t.ToAccountId)
			.HasColumnName(name: "to_account_id");

		builder.Property(propertyExpression: t => t.AmountFrom)
			.HasColumnName(name: "amount_from")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: t => t.CurrencyFrom)
			.HasColumnName(name: "currency_from")
			.HasMaxLength(maxLength: 3)
			.HasConversion(
				convertToProviderExpression: currency => currency.Value,
				convertFromProviderExpression: currency => Core.ValueObjects.Currency.Reconstitute(value: currency)
			);

		builder.Property(propertyExpression: t => t.AmountTo)
			.HasColumnName(name: "amount_to")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: t => t.CurrencyTo)
			.HasColumnName(name: "currency_to")
			.HasMaxLength(maxLength: 3)
			.HasConversion(
				convertToProviderExpression: currency => currency.Value,
				convertFromProviderExpression: currency => Core.ValueObjects.Currency.Reconstitute(value: currency)
			);

		builder.Property(propertyExpression: t => t.ExchangeRate)
			.HasColumnName(name: "exchange_rate")
			.HasColumnType(typeName: "numeric(18,6)");

		builder.Property(propertyExpression: t => t.Description)
			.HasColumnName(name: "description")
			.HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: t => t.OccurredAt)
			.HasColumnName(name: "occurred_at");

		builder.Property(propertyExpression: t => t.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.Property(propertyExpression: t => t.IsRatePending)
			.HasColumnName(name: "is_rate_pending");

		builder.Property(propertyExpression: t => t.RowVersion)
			.HasColumnName(name: "row_version")
			.HasDefaultValue(value: 0);

		builder.Property(propertyExpression: t => t.Status)
			.HasColumnName(name: "status")
			.HasMaxLength(maxLength: 20)
			.HasConversion(
				convertToProviderExpression: status => status.ToCode(),
				convertFromProviderExpression: value => value.FromCode()
			);

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<AccountEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.FromAccountId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<AccountEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.ToAccountId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.CurrencyFrom)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade)
			.HasPrincipalKey(keyExpression: c => c.Code);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.CurrencyTo)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}
