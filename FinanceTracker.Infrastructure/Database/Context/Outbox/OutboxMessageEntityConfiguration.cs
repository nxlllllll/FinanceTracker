using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Outbox;

public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
	public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
	{
		builder.ToTable(name: "outbox_messages");

		builder.HasKey(keyExpression: o => o.Id);

		builder.Property(propertyExpression: o => o.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: o => o.AggregateId)
			.HasColumnName(name: "aggregate_id");

		builder.Property(propertyExpression: o => o.AggregateType)
			.HasColumnName(name: "aggregate_type")
			.HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: o => o.Payload)
			.HasColumnName(name: "payload")
			.HasColumnType(typeName: "jsonb");

		builder.Property(propertyExpression: o => o.UpdatedAt)
			.HasColumnName(name: "updated_at");

		builder.Property(propertyExpression: o => o.ProcessedAt)
			.HasColumnName(name: "processed_at");
		
		builder.Property(propertyExpression: o => o.RetryCount)
			.HasColumnName(name: "retry_count")
			.HasDefaultValue(value: 0);

		builder.Property(propertyExpression: o => o.FailedAt)
			.HasColumnName(name: "failed_at");

		builder.Property(propertyExpression: o => o.LockedUntil)
			.HasColumnName(name: "locked_until");
	}
}