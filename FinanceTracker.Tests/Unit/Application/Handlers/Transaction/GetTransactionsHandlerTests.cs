using FinanceTracker.Application.Transactions.Queries.GetTransactions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
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
        Guid accountId = Guid.NewGuid();
        IReadOnlyList<TransactionDto> transactions = [TransactionFactory.Create(accountId: accountId), TransactionFactory.Create(accountId: accountId)];

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
        IReadOnlyList<TransactionDto> result = await _handler.Handle(
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

        await _handler.Handle(
            query: new GetTransactionsQuery(AccountId: Guid.NewGuid(), CategoryId: categoryId),
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
            query: new GetTransactionsQuery(AccountId: Guid.NewGuid(), Direction: DirectionType.Credit),
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
            query: new GetTransactionsQuery(AccountId: Guid.NewGuid(), IsExcluded: false),
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
        DateTime dateFrom = DateTime.UtcNow.AddDays(value: -7);
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

        await _handler.Handle(query: new GetTransactionsQuery(
            AccountId: Guid.NewGuid(),
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
}