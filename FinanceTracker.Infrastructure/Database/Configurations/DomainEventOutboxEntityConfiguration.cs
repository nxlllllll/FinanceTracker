using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class DomainEventOutboxEntityConfiguration : IEntityTypeConfiguration<DomainEventOutboxEntity>
{
	public void Configure(EntityTypeBuilder<DomainEventOutboxEntity> builder)
	{
		builder.ToTable(name: "domain_event_outbox");

		builder.HasKey(keyExpression: e => e.Id);

		builder.Property(propertyExpression: e => e.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: e => e.EventType)
			.HasColumnName(name: "event_type")
			.HasMaxLength(maxLength: 100);

		builder.Property(propertyExpression: e => e.AggregateId)
			.HasColumnName(name: "aggregate_id");

		builder.Property(propertyExpression: e => e.AggregateType)
			.HasColumnName(name: "aggregate_type")
			.HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: e => e.CorrelationId)
			.HasColumnName(name: "correlation_id");

		builder.Property(propertyExpression: e => e.Payload)
			.HasColumnName(name: "payload")
			.HasColumnType(typeName: "jsonb");

		builder.Property(propertyExpression: e => e.OccurredAt)
			.HasColumnName(name: "occurred_at");

		builder.Property(propertyExpression: e => e.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.Property(propertyExpression: e => e.ProcessedAt)
			.HasColumnName(name: "processed_at");

		builder.Property(propertyExpression: e => e.RetryCount)
			.HasColumnName(name: "retry_count")
			.HasDefaultValue(value: 0);

		builder.Property(propertyExpression: e => e.FailedAt)
			.HasColumnName(name: "failed_at");
	}
}