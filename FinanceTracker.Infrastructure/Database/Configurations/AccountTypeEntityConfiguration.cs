using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class AccountTypeEntityConfiguration : IEntityTypeConfiguration<AccountTypeEntity>
{
	public void Configure(EntityTypeBuilder<AccountTypeEntity> builder)
	{
		builder.ToTable(name: "account_types");

		builder.HasKey(keyExpression: a => a.Type);
		
		builder.Property(propertyExpression: a => a.Type)
			.HasColumnName(name: "type")
			.HasMaxLength(maxLength: 20);
		
		builder.Property(propertyExpression: a => a.Name)
			.HasColumnName(name: "name")
			.HasMaxLength(maxLength: 100);
		
		builder.Property(propertyExpression: a => a.Description)
			.HasColumnName(name: "description")
			.HasMaxLength(maxLength: 255);
	}
}