using FinanceTracker.Application.Transactions.Queries.GetTransactions;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Repositories.Transaction;
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

    private static FinanceTracker.Core.Domains.Transactions.Transaction CreateTransaction()
    {
        return FinanceTracker.Core.Domains.Transactions.Transaction.Create(
            accountId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: 1000m,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            description: null,
            occurredAt: DateTime.UtcNow
        );
    }

    [Test]
    public async Task Handle_ShouldReturnAllTransactions()
    {
        IReadOnlyList<FinanceTracker.Core.Domains.Transactions.Transaction> transactions = [CreateTransaction(), CreateTransaction()];

        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transactions);

        GetTransactionsQuery query = new GetTransactionsQuery(AccountId: Guid.NewGuid());
        IReadOnlyList<FinanceTracker.Core.Domains.Transactions.Transaction> result = await _handler.Handle(
            query: query,
            ct: CancellationToken.None
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task Handle_ShouldPassCategoryIdFilterToRepository()
    {
        Guid categoryId = Guid.NewGuid();

        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        GetTransactionsQuery query = new GetTransactionsQuery(AccountId: Guid.NewGuid(), CategoryId: categoryId);

        await _handler.Handle(query: query, ct: CancellationToken.None);

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

        GetTransactionsQuery query = new GetTransactionsQuery(AccountId: Guid.NewGuid(), Direction: DirectionType.Credit);

        await _handler.Handle(query: query, ct: CancellationToken.None);

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

        GetTransactionsQuery query = new GetTransactionsQuery(AccountId: Guid.NewGuid(), IsExcluded: false);

        await _handler.Handle(query: query, ct: CancellationToken.None);

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
        DateTime dateFrom = DateTime.UtcNow.AddDays(-7);
        DateTime dateTo = DateTime.UtcNow;

        _transactionReadRepository.GetAllAsync(
            accountId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid?>(),
            direction: Arg.Any<DirectionType?>(),
            isExcluded: Arg.Any<bool?>(),
            dateFrom: Arg.Any<DateTime?>(),
            dateTo: Arg.Any<DateTime?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        GetTransactionsQuery query = new GetTransactionsQuery(AccountId: Guid.NewGuid(), DateFrom: dateFrom, DateTo: dateTo);

        await _handler.Handle(query: query, ct: CancellationToken.None);

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
}