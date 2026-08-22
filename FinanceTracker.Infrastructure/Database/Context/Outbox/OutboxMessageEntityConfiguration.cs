using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Outbox;

public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
	public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
	{
		builder.ToTable(name: "outbox_messages");

		builder.HasKey(keyExpression: o => o.Id);

		builder.Property(propertyExpression: o => o.AggregateType).HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: o => o.Payload).HasColumnType(typeName: "jsonb");

		builder.Property(propertyExpression: o => o.RetryCount).HasDefaultValue(value: 0);
	}
}
