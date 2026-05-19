using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class UnresolvableEventTypeEntityConfiguration : IEntityTypeConfiguration<UnresolvableEventTypeEntity>
{
	public void Configure(EntityTypeBuilder<UnresolvableEventTypeEntity> builder)
	{
		builder.ToTable(name: "unresolvable_event_types");

		builder.HasKey(keyExpression: e => e.Code);

		builder.Property(propertyExpression: e => e.Code)
			.HasColumnName(name: "code")
			.HasMaxLength(maxLength: 50);

		builder.Property(propertyExpression: e => e.Name)
			.HasColumnName(name: "name")
			.HasMaxLength(maxLength: 100);
	}
}