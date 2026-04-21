using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database;

public sealed class FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options)
	: DbContext(options)
{
	public DbSet<EventEntity> Events => Set<EventEntity>();
	public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
	public DbSet<AccountBalanceEntity> AccountBalances => Set<AccountBalanceEntity>();
	public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
	public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
	public DbSet<CurrencyEntity> Currencies => Set<CurrencyEntity>();
	public DbSet<AccountTypeEntity> AccountTypes => Set<AccountTypeEntity>();
	public DbSet<UserEntity> Users => Set<UserEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
		=> modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerContext).Assembly);
}