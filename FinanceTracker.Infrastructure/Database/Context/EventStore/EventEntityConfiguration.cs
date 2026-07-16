using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.EventStore;

public sealed class EventEntityConfiguration : IEntityTypeConfiguration<EventEntity>
{
	/// <summary>
	/// Real constraint name from the migrations — used by EFUnitOfWork to recognize a
	/// version-conflict violation by name, not declared here via EF metadata.
	/// </summary>
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
	}
}
