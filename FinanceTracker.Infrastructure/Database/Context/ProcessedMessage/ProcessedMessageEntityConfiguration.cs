using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;

public sealed class ProcessedMessageEntityConfiguration : IEntityTypeConfiguration<ProcessedMessageEntity>
{
	public void Configure(EntityTypeBuilder<ProcessedMessageEntity> builder)
	{
		builder.ToTable(name: "processed_messages");

		builder.HasKey(keyExpression: e => new { e.MessageId, e.ConsumerType });

		builder.Property(propertyExpression: e => e.ConsumerType).HasMaxLength(maxLength: 100);
	}
}
