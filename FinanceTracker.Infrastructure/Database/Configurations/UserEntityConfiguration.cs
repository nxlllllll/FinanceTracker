using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Configurations;

public sealed class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
	public void Configure(EntityTypeBuilder<UserEntity> builder)
	{
		builder.ToTable(name: "users");

		builder.HasKey(keyExpression: u => u.Id);

		builder.Property(propertyExpression: u => u.Email)
			.HasColumnName(name: "email")
			.HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: u => u.PasswordHash)
			.HasColumnName(name: "password_hash")
			.HasMaxLength(maxLength: 255);

		builder.Property(propertyExpression: u => u.BaseCurrencyCode)
			.HasColumnName(name: "base_currency_code")
			.HasMaxLength(maxLength: 3);

		builder.Property(propertyExpression: u => u.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.HasIndex(indexExpression: u => u.Email).IsUnique();

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: u => u.BaseCurrencyCode)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict);
	}
}