using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Operation;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Operations;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Operations;

public sealed class OperationsWriteRepositoryTests : DatabaseFixture
{
    private OperationsWriteRepository _repository = null!;
    private UserBuilder _userBuilder = null!;
    private AccountBuilder _accountBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _repository = new OperationsWriteRepository(context: Context);
        _userBuilder = new UserBuilder(context: Context);
        _accountBuilder = new AccountBuilder(context: Context);
        _categoryBuilder = new CategoryBuilder(context: Context);
    }

    private async Task<(Guid UserId, Guid AccountId, Guid CategoryId)> CreatePrerequisitesAsync()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        return (userId, accountId, categoryId);
    }

    private Core.Domains.Transaction.Transaction CreateTransaction(
        Guid userId,
        Guid accountId,
        Guid categoryId,
        DirectionType direction = DirectionType.Debit,
        bool isExcluded = false,
        string? description = null)
    {
        return Core.Domains.Transaction.Transaction.Reconstitute(
            id: Guid.CreateVersion7(),
            accountId: accountId,
            userId: userId,
            categoryId: categoryId,
            amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            direction: direction,
            exchangeRate: 1m,
            isExcluded: isExcluded,
            description: description,
            isRatePending: false,
            occurredAt: FakeDateProvider.Default.UtcNow
        );
    }

    private Core.Domains.Transfer.Transfer CreateTransfer(
        Guid userId,
        Guid fromAccountId,
        Guid toAccountId,
        string? description = null)
    {
        return Core.Domains.Transfer.Transfer.Create(
            userId: userId,
            fromAccountId: fromAccountId,
            toAccountId: toAccountId,
            amountFrom: 1000m,
            currencyFrom: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            amountTo: 900m,
            currencyTo: Core.ValueObjects.Currency.Create(value: "RUB").Value,
            exchangeRate: 0.9m,
            isRatePending: false,
            description: description,
            occurredAt: FakeDateProvider.Default.UtcNow
        ).Value!;
    }

    [Test]
    public async Task CreateFromTransactionAsync_ShouldCreateOperationWithTransactionType()
    {
        (Guid userId, Guid accountId, Guid categoryId) = await CreatePrerequisitesAsync();
        Core.Domains.Transaction.Transaction transaction = CreateTransaction(
            userId: userId, 
            accountId: accountId, 
            categoryId: categoryId
        );

        await _repository.CreateFromTransactionAsync(transaction: transaction);

        bool exists = await Context.Operations.AnyAsync(predicate: o => o.Id == transaction.Id && o.Type == OperationType.Transaction);
        await Assert.That(value: exists).IsTrue();
    }

    [Test]
    public async Task CreateFromTransactionAsync_ShouldSerializePayloadCorrectly()
    {
        (Guid userId, Guid accountId, Guid categoryId) = await CreatePrerequisitesAsync();
        Core.Domains.Transaction.Transaction transaction = CreateTransaction(
            userId: userId, 
            accountId: accountId, 
            categoryId: categoryId,
            direction: DirectionType.Credit,
            description: "Зарплата"
        );

        await _repository.CreateFromTransactionAsync(transaction: transaction);

        string payload = await Context.Operations
            .Where(predicate: o => o.Id == transaction.Id)
            .Select(selector: o => o.Payload)
            .FirstAsync();

        TransactionPayload deserialized = JsonSerializer.Deserialize<TransactionPayload>(
            json: payload, 
            options: FinanceTrackerJsonOptions.Payload
        )!;

        await Assert.That(value: deserialized.AccountId).IsEqualTo(expected: accountId);
        await Assert.That(value: deserialized.CategoryId).IsEqualTo(expected: categoryId);
        await Assert.That(value: deserialized.Amount).IsEqualTo(expected: 1000m);
        await Assert.That(value: deserialized.Direction).IsEqualTo(expected: DirectionType.Credit);
        await Assert.That(value: deserialized.IsExcluded).IsFalse();
    }

    [Test]
    public async Task CreateFromTransferAsync_ShouldCreateOperationWithTransferType()
    {
        (Guid userId, Guid fromAccountId, _) = await CreatePrerequisitesAsync();
        Guid toAccountId = await _accountBuilder.CreateAsync(userId: userId);
        Core.Domains.Transfer.Transfer transfer = CreateTransfer(
            userId: userId, 
            fromAccountId: fromAccountId,
            toAccountId: toAccountId
        );

        await _repository.CreateFromTransferAsync(transfer: transfer);

        bool exists = await Context.Operations.AnyAsync(predicate: o => o.Id == transfer.Id && o.Type == OperationType.Transfer);
        await Assert.That(value: exists).IsTrue();
    }

    [Test]
    public async Task CreateFromTransferAsync_ShouldSerializePayloadCorrectly()
    {
        (Guid userId, Guid fromAccountId, _) = await CreatePrerequisitesAsync();
        Guid toAccountId = await _accountBuilder.CreateAsync(userId: userId);
        Core.Domains.Transfer.Transfer transfer = CreateTransfer(
            userId: userId, 
            fromAccountId: fromAccountId, 
            toAccountId: toAccountId,
            description: "Перевод"
        );

        await _repository.CreateFromTransferAsync(transfer: transfer);

        string payload = await Context.Operations
            .Where(predicate: o => o.Id == transfer.Id)
            .Select(selector: o => o.Payload)
            .FirstAsync();

        TransferPayload deserialized = JsonSerializer.Deserialize<TransferPayload>(
            json: payload,
            options: FinanceTrackerJsonOptions.Payload
        )!;

        await Assert.That(value: deserialized.FromAccountId).IsEqualTo(expected: fromAccountId);
        await Assert.That(value: deserialized.ToAccountId).IsEqualTo(expected: toAccountId);
        await Assert.That(value: deserialized.AmountFrom).IsEqualTo(expected: 1000m);
        await Assert.That(value: deserialized.AmountTo).IsEqualTo(expected: 900m);
    }

    [Test]
    public async Task UpdateCategoryAsync_ShouldUpdateCategoryIdInPayload()
    {
        (Guid userId, Guid accountId, Guid categoryId) = await CreatePrerequisitesAsync();
        Core.Domains.Transaction.Transaction transaction = CreateTransaction(
            userId: userId, 
            accountId: accountId,
            categoryId: categoryId
        );
        await _repository.CreateFromTransactionAsync(transaction: transaction);

        Guid newCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
        await _repository.UpdateCategoryAsync(operationId: transaction.Id, categoryId: newCategoryId);

        string payload = await Context.Operations
            .Where(predicate: o => o.Id == transaction.Id)
            .Select(selector: o => o.Payload)
            .FirstAsync();

        TransactionPayload deserialized = JsonSerializer.Deserialize<TransactionPayload>(
            json: payload,
            options: FinanceTrackerJsonOptions.Payload
        )!;

        await Assert.That(value: deserialized.CategoryId).IsEqualTo(expected: newCategoryId);
    }

    [Test]
    public async Task UpdateIsExcludedAsync_WhenTrue_ShouldSetIsExcludedInPayload()
    {
        (Guid userId, Guid accountId, Guid categoryId) = await CreatePrerequisitesAsync();
        Core.Domains.Transaction.Transaction transaction = CreateTransaction(
            userId: userId, 
            accountId: accountId,
            categoryId: categoryId,
            isExcluded: false
        );
        await _repository.CreateFromTransactionAsync(transaction: transaction);

        await _repository.UpdateIsExcludedAsync(operationId: transaction.Id, isExcluded: true);

        string payload = await Context.Operations
            .Where(predicate: o => o.Id == transaction.Id)
            .Select(selector: o => o.Payload)
            .FirstAsync();

        TransactionPayload deserialized = JsonSerializer.Deserialize<TransactionPayload>(
            json: payload, 
            options: FinanceTrackerJsonOptions.Payload
        )!;

        await Assert.That(value: deserialized.IsExcluded).IsTrue();
    }

    [Test]
    public async Task UpdateIsExcludedAsync_WhenFalse_ShouldClearIsExcludedInPayload()
    {
        (Guid userId, Guid accountId, Guid categoryId) = await CreatePrerequisitesAsync();
        Core.Domains.Transaction.Transaction transaction = CreateTransaction(
            userId: userId, 
            accountId: accountId,
            categoryId: categoryId,
            isExcluded: true
        );
        await _repository.CreateFromTransactionAsync(transaction: transaction);

        await _repository.UpdateIsExcludedAsync(operationId: transaction.Id, isExcluded: false);

        string payload = await Context.Operations
            .Where(predicate: o => o.Id == transaction.Id)
            .Select(selector: o => o.Payload)
            .FirstAsync();

        TransactionPayload deserialized = JsonSerializer.Deserialize<TransactionPayload>(
            json: payload, 
            options: FinanceTrackerJsonOptions.Payload
        )!;

        await Assert.That(value: deserialized.IsExcluded).IsFalse();
    }

    [Test]
    public async Task UpdateDescriptionAsync_ShouldUpdateDescriptionColumn()
    {
        (Guid userId, Guid accountId, Guid categoryId) = await CreatePrerequisitesAsync();
        Core.Domains.Transaction.Transaction transaction = CreateTransaction(
            userId: userId, 
            accountId: accountId,
            categoryId: categoryId,
            description: "Старое"
        );
        await _repository.CreateFromTransactionAsync(transaction: transaction);

        await _repository.UpdateDescriptionAsync(operationId: transaction.Id, description: "Новое");

        string? description = await Context.Operations
            .Where(predicate: o => o.Id == transaction.Id)
            .Select(selector: o => o.Description)
            .FirstAsync();

        await Assert.That(value: description).IsEqualTo(expected: "Новое");
    }

    [Test]
    public async Task UpdateDescriptionAsync_WhenNull_ShouldClearDescription()
    {
        (Guid userId, Guid accountId, Guid categoryId) = await CreatePrerequisitesAsync();
        Core.Domains.Transaction.Transaction transaction = CreateTransaction(
            userId: userId, 
            accountId: accountId,
            categoryId: categoryId,
            description: "Описание"
        );
        await _repository.CreateFromTransactionAsync(transaction: transaction);

        await _repository.UpdateDescriptionAsync(operationId: transaction.Id, description: null);

        string? description = await Context.Operations
            .Where(predicate: o => o.Id == transaction.Id)
            .Select(selector: o => o.Description)
            .FirstAsync();

        await Assert.That(value: description).IsNull();
    }
}
