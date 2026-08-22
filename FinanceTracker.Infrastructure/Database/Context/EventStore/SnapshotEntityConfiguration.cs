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

		builder.Property(propertyExpression: s => s.AggregateType).HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: s => s.State).HasColumnType(typeName: "jsonb");
	}
}
