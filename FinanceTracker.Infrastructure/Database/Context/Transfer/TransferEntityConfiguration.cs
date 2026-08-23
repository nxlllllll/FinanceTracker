using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Context.User;
using FinanceTracker.Infrastructure.Database.Converters;
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

		builder.Property(propertyExpression: t => t.AmountFrom).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: t => t.AmountTo).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: t => t.ExchangeRate).HasColumnType(typeName: "numeric(18,6)");

		builder.Property(propertyExpression: t => t.RateStatus)
			.HasMaxLength(maxLength: 16)
			.HasConversion<SnakeCaseEnumConverter<RateStatus>>();

		builder.Property(propertyExpression: t => t.Description).HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: t => t.RowVersion).HasDefaultValue(value: 0);

		builder.Property(propertyExpression: t => t.Status)
			.HasMaxLength(maxLength: 20)
			.HasConversion(
				convertToProviderExpression: status => status.ToCode(),
				convertFromProviderExpression: value => value.FromCode()
			);

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: t => t.UserId)
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
