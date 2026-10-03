using FinanceTracker.Core.Domains.User;
using FinanceTracker.Infrastructure.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Notification;

public sealed class NotificationDeliveryEntityConfiguration : IEntityTypeConfiguration<NotificationDeliveryEntity>
{
	public void Configure(EntityTypeBuilder<NotificationDeliveryEntity> builder)
	{
		builder.ToTable(name: "notification_deliveries");

		builder.HasKey(keyExpression: d => new { d.EventId, d.NotificationType });

		builder.Property(propertyExpression: d => d.NotificationType)
			.HasMaxLength(maxLength: 16)
			.HasConversion<SnakeCaseEnumConverter<NotificationType>>();
	}
}
