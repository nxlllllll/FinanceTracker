using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<EventEntity>
{
	public void Configure(EntityTypeBuilder<EventEntity> builder)
	{
		builder.ToTable("events");

		builder.HasKey(e => e.Id);

		builder.Property(e => e.AggregateId)
			.HasColumnName("aggregate_id");

		builder.Property(e => e.AggregateType)
			.HasColumnName("aggregate_type")
			.HasMaxLength(50);

		builder.Property(e => e.EventType)
			.HasColumnName("event_type")
			.HasMaxLength(100);

		builder.Property(e => e.Version)
			.HasColumnName("version");

		builder.Property(e => e.Payload)
			.HasColumnName("payload")
			.HasColumnType("jsonb");

		builder.Property(e => e.OccurredAt)
			.HasColumnName("occurred_at");

		builder.Property(e => e.CreatedAt)
			.HasColumnName("created_at");

		builder.HasIndex(e => new { e.AggregateId, e.Version })
			.IsUnique();
	}
}