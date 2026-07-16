using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.EventStore;

public sealed class SnapshotEntityConfiguration : IEntityTypeConfiguration<SnapshotEntity>
{
	public void Configure(EntityTypeBuilder<SnapshotEntity> builder)
	{
		builder.ToTable(name: "snapshots");

		builder.HasKey(keyExpression: s => new
		{
			s.AggregateId,
			s.Version
		});

		builder.Property(propertyExpression: s => s.AggregateId)
			.HasColumnName(name: "aggregate_id");

		builder.Property(propertyExpression: s => s.AggregateType)
			.HasColumnName(name: "aggregate_type")
			.HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: s => s.Version)
			.HasColumnName(name: "version");

		builder.Property(propertyExpression: s => s.State)
			.HasColumnName(name: "state")
			.HasColumnType(typeName: "jsonb");

		builder.Property(propertyExpression: s => s.CreatedAt)
			.HasColumnName(name: "created_at");
	}
}
