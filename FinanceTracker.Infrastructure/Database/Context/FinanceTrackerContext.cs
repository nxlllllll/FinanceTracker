using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Context;

public sealed class FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options) : DbContext(options)
{
	public DbSet<EventEntity> Events => Set<EventEntity>();
	
	public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
	
	public DbSet<AccountBalanceEntity> AccountBalances => Set<AccountBalanceEntity>();
	
	public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
	
	public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
	
	public DbSet<CurrencyEntity> Currencies => Set<CurrencyEntity>();
	
	public DbSet<AccountTypeEntity> AccountTypes => Set<AccountTypeEntity>();
	
	public DbSet<UserEntity> Users => Set<UserEntity>();
	
	public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
	
	public DbSet<CurrencyRateEntity> CurrencyRates => Set<CurrencyRateEntity>();

	public DbSet<SnapshotEntity> Snapshots => Set<SnapshotEntity>();

	public DbSet<TransferEntity> Transfers => Set<TransferEntity>();
	
	public DbSet<CategoryTotalEntity> CategoryTotals => Set<CategoryTotalEntity>();

	public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();
	
	public DbSet<BudgetProgressEntity> BudgetProgresses => Set<BudgetProgressEntity>();
	
	public DbSet<RecurringTransactionEntity> RecurringTransactions => Set<RecurringTransactionEntity>();
	
	protected override void OnModelCreating(ModelBuilder modelBuilder)
		=> modelBuilder.ApplyConfigurationsFromAssembly(assembly: typeof(FinanceTrackerContext).Assembly);
}