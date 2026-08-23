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

		builder.Property(propertyExpression: e => e.AggregateType).HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: e => e.EventType).HasMaxLength(maxLength: 100);

		builder.Property(propertyExpression: e => e.Payload).HasColumnType(typeName: "jsonb");
	}
}
