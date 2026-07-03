using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Database.Context.Account;

public sealed class AccountEntityConfiguration : IEntityTypeConfiguration<AccountEntity>
{
	public void Configure(EntityTypeBuilder<AccountEntity> builder)
	{
		builder.ToTable(name: "accounts");

		builder.HasKey(keyExpression: a => a.Id);

		builder.Property(propertyExpression: a => a.Id)
			.HasColumnName(name: "id");

		builder.Property(propertyExpression: a => a.UserId)
			.HasColumnName(name: "user_id");

		builder.Property(propertyExpression: a => a.Name)
			.HasColumnName(name: "name")
			.HasMaxLength(maxLength: 100)
			.HasConversion(
				convertToProviderExpression: name => name.Value,
				convertFromProviderExpression: name => Name.Reconstitute(value: name)
			);

		builder.Property(propertyExpression: a => a.AccountType)
			.HasColumnName(name: "account_type_code")
			.HasMaxLength(maxLength: 20)
			.HasConversion(
				convertToProviderExpression: type => type.ToString().ToLowerInvariant(),
				convertFromProviderExpression: value => Enum.Parse<AccountType>(value: value, ignoreCase: true)
			);

		builder.Property(propertyExpression: a => a.Currency)
			.HasColumnName(name: "currency_code")
			.HasMaxLength(maxLength: 3)
			.HasConversion(
				convertToProviderExpression: currency => currency.Value,
				convertFromProviderExpression: currency => Core.ValueObjects.Currency.Reconstitute(value: currency)
			);

		builder.Property(propertyExpression: a => a.IsArchived)
			.HasColumnName(name: "is_archived");

		builder.Property(propertyExpression: a => a.CreatedAt)
			.HasColumnName(name: "created_at");

		builder.HasOne<AccountBalanceEntity>().WithOne()
			.HasForeignKey<AccountBalanceEntity>(foreignKeyExpression: b => b.AccountId);

		builder.HasOne<UserEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: a => a.UserId)
			.OnDelete(deleteBehavior: DeleteBehavior.Cascade);

		builder.HasOne<CurrencyEntity>().WithMany()
			.HasForeignKey(foreignKeyExpression: b => b.Currency)
			.OnDelete(deleteBehavior: DeleteBehavior.Restrict)
			.HasPrincipalKey(keyExpression: c => c.Code);
	}
}
