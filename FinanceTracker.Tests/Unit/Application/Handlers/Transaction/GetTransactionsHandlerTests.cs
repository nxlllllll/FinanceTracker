using FinanceTracker.Application.UseCases.Transactions.Queries.GetTransactions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class GetTransactionsHandlerTests
{
    private ITransactionReadRepository _transactionReadRepository = null!;
    private GetTransactionsHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionReadRepository = Substitute.For<ITransactionReadRepository>();
        _handler = new GetTransactionsHandler(transactionReadRepository: _transactionReadRepository);
    }

    [Test]
    public async Task Handle_ShouldReturnAllTransactions()
    {
        Guid accountId = Guid.CreateVersion7();
        IReadOnlyList<FinanceTracker.Core.Domains.Transaction.Transaction> transactions = [
            TransactionFactory.Create(accountId: accountId), 
            TransactionFactory.Create(accountId: accountId)
        ];

        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transactions);

        GetTransactionsQuery query = new GetTransactionsQuery(AccountId: accountId);
        IReadOnlyList<FinanceTracker.Core.Domains.Transaction.Transaction> result = await _handler.Handle(
            query: query,
            ct: CancellationToken.None
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task Handle_ShouldPassCategoryIdFilterToRepository()
    {
        Guid categoryId = Guid.CreateVersion7();

        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _handler.Handle(
            query: new GetTransactionsQuery(AccountId: Guid.CreateVersion7(), CategoryId: categoryId),
            ct: CancellationToken.None
        );

        await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: categoryId,
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_ShouldPassDirectionFilterToRepository()
    {
        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _handler.Handle(
            query: new GetTransactionsQuery(AccountId: Guid.CreateVersion7(), Direction: DirectionType.Credit),
            ct: CancellationToken.None
        );

        await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: DirectionType.Credit,
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_ShouldPassIsExcludedFilterToRepository()
    {
        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _handler.Handle(
            query: new GetTransactionsQuery(AccountId: Guid.CreateVersion7(), IsExcluded: false),
            ct: CancellationToken.None
        );

        await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: false,
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_ShouldPassDateRangeFilterToRepository()
    {
        DateTime dateFrom = FakeDateProvider.Default.UtcNow.AddDays(value: -7);
        DateTime dateTo = FakeDateProvider.Default.UtcNow;

        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _handler.Handle(query: new GetTransactionsQuery(
            AccountId: Guid.CreateVersion7(),
            DateFrom: dateFrom,
            DateTo: dateTo
        ), ct: CancellationToken.None);

        await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: dateFrom,
            dateTo: dateTo,
            ct: Arg.Any<CancellationToken>()
        );
    }
    
    [Test]
    public async Task Handle_WhenNoTransactions_ShouldReturnEmptyList()
    {
        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        IReadOnlyList<FinanceTracker.Core.Domains.Transaction.Transaction> result = await _handler.Handle(
            query: new GetTransactionsQuery(AccountId: Guid.CreateVersion7()),
            ct: CancellationToken.None
        );

        await Assert.That(value: result).IsEmpty();
    }

    [Test]
    public async Task Handle_ShouldPassAllFiltersToRepository()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid categoryId = Guid.CreateVersion7();
        DateTime dateFrom = FakeDateProvider.Default.UtcNow.AddDays(value: -30);
        DateTime dateTo = FakeDateProvider.Default.UtcNow;

        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _handler.Handle(query: new GetTransactionsQuery(
            AccountId: accountId,
            CategoryId: categoryId,
            Direction: DirectionType.Debit,
            IsExcluded: false,
            DateFrom: dateFrom,
            DateTo: dateTo
        ), ct: CancellationToken.None);

        await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            accountId: accountId,
            categoryId: categoryId,
            direction: DirectionType.Debit,
            isExcluded: false,
            dateFrom: dateFrom,
            dateTo: dateTo,
            ct: Arg.Any<CancellationToken>()
        );
    }
}