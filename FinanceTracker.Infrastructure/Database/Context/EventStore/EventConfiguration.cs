using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.EventStore;

public sealed class EventEntityConfiguration : IEntityTypeConfiguration<EventEntity>
{
	public const string VersionConstraint = "uq_events_aggregate_version";
	
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

        builder.Property(propertyExpression: e => e.CorrelationId)
            .HasColumnName(name: "correlation_id");

        builder.Property(propertyExpression: e => e.Payload)
            .HasColumnName(name: "payload")
            .HasColumnType(typeName: "jsonb");

        builder.Property(propertyExpression: e => e.OccurredAt)
            .HasColumnName(name: "occurred_at");

        builder.Property(propertyExpression: e => e.CreatedAt)
            .HasColumnName(name: "created_at");

        builder.Property(propertyExpression: e => e.SchemaVersion)
            .HasColumnName(name: "schema_version");

        builder.HasIndex(indexExpression: e => new { e.AggregateId, e.Version })
            .HasDatabaseName(name: "idx_events_aggregate_id");

        builder.HasIndex(indexExpression: e => e.CorrelationId)
            .HasDatabaseName(name: "idx_events_correlation_id")
            .HasFilter(sql: "correlation_id is not null");

        builder.HasAlternateKey(keyExpression: e => new { e.AggregateId, e.Version })
            .HasName(name: VersionConstraint);
    }
}
