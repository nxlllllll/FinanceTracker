using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Operation;

public sealed class OperationEntityConfiguration : IEntityTypeConfiguration<OperationEntity>
{
	public void Configure(EntityTypeBuilder<OperationEntity> builder)
	{
		builder.ToTable(name: "rm_operations");

		builder.HasKey(keyExpression: o => new { o.UserId, o.OccurredAt, o.Id });

		builder.Property(propertyExpression: o => o.Type).HasMaxLength(maxLength: 12);

		builder.Property(propertyExpression: o => o.Description).HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: o => o.Amount).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: o => o.CurrencyCode).HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: o => o.DirectionType).HasMaxLength(maxLength: 10);

		builder.Property(propertyExpression: o => o.AmountFrom).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: o => o.CurrencyFrom).HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: o => o.AmountTo).HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: o => o.CurrencyTo).HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: o => o.Status).HasMaxLength(maxLength: 20);
	}
}
