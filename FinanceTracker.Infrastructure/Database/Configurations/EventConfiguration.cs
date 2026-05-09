using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<EventEntity>
{
	public void Configure(EntityTypeBuilder<EventEntity> builder)
	{
		builder.ToTable(name: "events");

		builder.HasKey(keyExpression: e => e.Id);
		
		builder.Property(propertyExpression: a => a.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: e => e.AggregateId)
			.HasColumnName(name: "aggregate_id");

		builder.Property(propertyExpression: e => e.AggregateType)
			.HasColumnName(name: "aggregate_type")
			.HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: e => e.EventType)
			.HasColumnName(name: "event_type")
			.HasMaxLength(maxLength: 100);

		builder.Property(propertyExpression: e => e.Version)
			.HasColumnName(name: "version");

		builder.Property(propertyExpression: e => e.Payload)
			.HasColumnName(name: "payload")
			.HasColumnType(typeName: "jsonb");

		builder.Property(propertyExpression: e => e.OccurredAt)
			.HasColumnName(name: "occurred_at");

		builder.Property(propertyExpression: e => e.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.HasIndex(indexExpression: e => new { e.AggregateId, e.Version })
			.IsUnique();
	}
}