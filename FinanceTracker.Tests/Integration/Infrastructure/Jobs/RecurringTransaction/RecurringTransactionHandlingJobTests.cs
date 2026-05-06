using FinanceTracker.Application.UseCases.RecurringTransactions.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Infrastructure.Database.Jobs.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Jobs.RecurringTransaction;

public sealed class RecurringTransactionHandlingJobTests : DatabaseFixture
{
    private UserBuilder _userBuilder = null!;
    private AccountBuilder _accountBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;
    private RecurringTransactionBuilder _recurringTransactionBuilder = null!;
    private INotificationDispatcher _dispatcher = null!;
    private RecurringTransactionHandlingJob _job = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _userBuilder = new UserBuilder(context: Context);
        _accountBuilder = new AccountBuilder(context: Context);
        _categoryBuilder = new CategoryBuilder(context: Context);
        _recurringTransactionBuilder = new RecurringTransactionBuilder(context: Context);
        _dispatcher = Substitute.For<INotificationDispatcher>();

        _job = new RecurringTransactionHandlingJob(
            recurringTransactionReadRepository: new RecurringTransactionReadRepository(context: Context),
            recurringTransactionWriteRepository: new RecurringTransactionWriteRepository(context: Context, dateProvider: FakeDateProvider.Default),
            notificationDispatcher: _dispatcher,
            unitOfWork: new EFUnitOfWork(
                context: Context,
                logger: Substitute.For<ILogger<EFUnitOfWork>>()
            ),
            dateProvider: new FinanceTracker.Infrastructure.Services.DateProvider(),
            factory: new TransactionNotificationFactory(),
            logger: Substitute.For<ILogger<RecurringTransactionHandlingJob>>()
        );
    }

    [Test]
    public async Task ProcessAsync_WhenNoDueTransactions_ShouldNotDispatch()
    {
        await _job.ProcessTransactionsAsync(ct: CancellationToken.None);

        await _dispatcher.DidNotReceive().DispatchAsync(
            appNotification: Arg.Any<IAppNotification>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task ProcessAsync_WhenDueTransactionExists_ShouldDispatchNotification()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        await _recurringTransactionBuilder.CreateAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            dayOfMonth: DateTime.UtcNow.Day
        );

        await _job.ProcessTransactionsAsync(ct: CancellationToken.None);

        await _dispatcher.Received(requiredNumberOfCalls: 1).DispatchAsync(
            appNotification: Arg.Any<IAppNotification>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task ProcessAsync_WhenDueTransactionExists_ShouldMarkAsExecuted()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        Guid id = await _recurringTransactionBuilder.CreateAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            dayOfMonth: DateTime.UtcNow.Day
        );

        await _job.ProcessTransactionsAsync(ct: CancellationToken.None);

        FinanceTracker.Infrastructure.Database.Entities.RecurringTransactionEntity? entity =
            await Context.RecurringTransactions.AsNoTracking().FirstOrDefaultAsync(predicate: r => r.Id == id);

        await Assert.That(value: entity!.LastExecutedAt).IsNotNull();
    }

    [Test]
    public async Task ProcessAsync_WhenTransactionAlreadyExecutedThisMonth_ShouldNotDispatch()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        Guid id = await _recurringTransactionBuilder.CreateAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            dayOfMonth: DateTime.UtcNow.Day
        );

        await Context.RecurringTransactions.Where(predicate: r => r.Id == id)
            .ExecuteUpdateAsync(setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: r => r.LastExecutedAt,
                valueExpression: new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 12, 0, 0, DateTimeKind.Utc)
            ));

        await _job.ProcessTransactionsAsync(ct: CancellationToken.None);

        await _dispatcher.DidNotReceive().DispatchAsync(
            appNotification: Arg.Any<IAppNotification>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task ProcessAsync_WhenDispatcherFails_ShouldContinueWithOtherTransactions()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        await _recurringTransactionBuilder.CreateAsync(
            userId: userId, 
            accountId: accountId,
            categoryId: categoryId,
            dayOfMonth: DateTime.UtcNow.Day
        );
        await _recurringTransactionBuilder.CreateAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            dayOfMonth: DateTime.UtcNow.Day
        );

        int callCount = 0;
        _dispatcher.DispatchAsync(
            appNotification: Arg.Any<IAppNotification>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: _ =>
        {
            callCount++;
            if (callCount == 1)
                throw new InvalidOperationException(message: "Simulated failure");
            return Task.CompletedTask;
        });

        await _job.ProcessTransactionsAsync(ct: CancellationToken.None);

        await _dispatcher.Received(requiredNumberOfCalls: 2).DispatchAsync(
            appNotification: Arg.Any<IAppNotification>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}