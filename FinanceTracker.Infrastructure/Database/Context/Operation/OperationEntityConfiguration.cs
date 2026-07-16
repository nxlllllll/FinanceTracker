using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Operation;

public sealed class OperationEntityConfiguration : IEntityTypeConfiguration<OperationEntity>
{
	public void Configure(EntityTypeBuilder<OperationEntity> builder)
	{
		builder.ToTable(name: "rm_operations");

		builder.HasKey(keyExpression: o => new { o.UserId, o.OccurredAt, o.Id });

		builder.Property(propertyExpression: o => o.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: o => o.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: o => o.Type)
			.HasColumnName(name: "type").HasMaxLength(maxLength: 12);

		builder.Property(propertyExpression: o => o.OccurredAt)
			.HasColumnName(name: "occurred_at");

		builder.Property(propertyExpression: o => o.Description)
			.HasColumnName(name: "description").HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: o => o.AccountId)
			.HasColumnName(name: "account_id");

		builder.Property(propertyExpression: o => o.CategoryId)
			.HasColumnName(name: "category_id");

		builder.Property(propertyExpression: o => o.Amount)
			.HasColumnName(name: "amount")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: o => o.CurrencyCode)
			.HasColumnName(name: "currency_code")
			.HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: o => o.DirectionType)
			.HasColumnName(name: "direction_type")
			.HasMaxLength(maxLength: 10);

		builder.Property(propertyExpression: o => o.IsExcluded)
			.HasColumnName(name: "is_excluded");

		builder.Property(propertyExpression: o => o.FromAccountId)
			.HasColumnName(name: "from_account_id");

		builder.Property(propertyExpression: o => o.ToAccountId)
			.HasColumnName(name: "to_account_id");

		builder.Property(propertyExpression: o => o.AmountFrom)
			.HasColumnName(name: "amount_from")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: o => o.CurrencyFrom)
			.HasColumnName(name: "currency_from")
			.HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: o => o.AmountTo)
			.HasColumnName(name: "amount_to")
			.HasColumnType(typeName: "numeric(18,2)");

		builder.Property(propertyExpression: o => o.CurrencyTo)
			.HasColumnName(name: "currency_to")
			.HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: o => o.Status)
			.HasColumnName(name: "status")
			.HasMaxLength(maxLength: 20);
	}
}
