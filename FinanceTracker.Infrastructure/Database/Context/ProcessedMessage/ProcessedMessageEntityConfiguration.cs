using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;

public sealed class ProcessedMessageEntityConfiguration : IEntityTypeConfiguration<ProcessedMessageEntity>
{
	public void Configure(EntityTypeBuilder<ProcessedMessageEntity> builder)
	{
		builder.ToTable(name: "processed_messages");

		builder.HasKey(keyExpression: e => new { e.MessageId, e.ConsumerType });

		builder.Property(propertyExpression: e => e.MessageId)
			.HasColumnName(name: "message_id");

		builder.Property(propertyExpression: e => e.ConsumerType)
			.HasColumnName(name: "consumer_type")
			.HasMaxLength(maxLength: 100);

		builder.Property(propertyExpression: e => e.ProcessedAt)
			.HasColumnName(name: "processed_at");
	}
}