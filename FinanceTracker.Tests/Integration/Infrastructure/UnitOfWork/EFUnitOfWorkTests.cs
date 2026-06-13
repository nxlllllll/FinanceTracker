using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.UnitOfWork;

public sealed class EFUnitOfWorkTests : DatabaseFixture
{
    private EFUnitOfWork _unitOfWork = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _unitOfWork = new EFUnitOfWork(
            context: Context,
            logger: Substitute.For<ILogger<EFUnitOfWork>>()
        );
    }

    [After(hookType: Test)]
    public async Task CloseAsync()
        => await _unitOfWork.DisposeAsync();

    [Test]
    public async Task BeginAndCommit_ShouldPersistChanges()
    {
        await _unitOfWork.BeginTransactionAsync();
        Context.Currencies.Add(new CurrencyEntity
        {
            Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
            Name = "Test",
            Symbol = "T",
            IsActive = true
        });
        await Context.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task BeginAndRollback_ShouldDiscardChanges()
    {
        await _unitOfWork.BeginTransactionAsync();
        Context.Currencies.Add(new CurrencyEntity
        {
            Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
            Name = "Test",
            Symbol = "T",
            IsActive = true
        });
        await Context.SaveChangesAsync();
        await _unitOfWork.RollbackAsync();

        int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task NestedBegin_ShouldCreateSavepoint()
    {
        await _unitOfWork.BeginTransactionAsync();

        Context.Currencies.Add(new CurrencyEntity
        {
            Code = Core.ValueObjects.Currency.Create(value: "OUT").Value,
            Name = "Output",
            Symbol = "O",
            IsActive = true
        });

        await Context.SaveChangesAsync();

        await _unitOfWork.BeginTransactionAsync();
        Context.Currencies.Add(new CurrencyEntity
        {
            Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
            Name = "Test",
            Symbol = "T",
            IsActive = true
        });
        await Context.SaveChangesAsync();
        await _unitOfWork.RollbackAsync();

        await _unitOfWork.CommitAsync();

        int outCount = await Context.Currencies.CountAsync(predicate: c => c.Code == "OUT");
        int innCount = await Context.Currencies.CountAsync(predicate: c => c.Code == "INN");

        await Assert.That(value: outCount).IsEqualTo(expected: 1);
        await Assert.That(value: innCount).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task NestedBeginAndCommit_ShouldPersistBothLevels()
    {
        await _unitOfWork.BeginTransactionAsync();

        Context.Currencies.Add(new CurrencyEntity
        {
            Code = Core.ValueObjects.Currency.Create(value: "OUT").Value,
            Name = "Output",
            Symbol = "O",
            IsActive = true
        });
        await Context.SaveChangesAsync();

        await _unitOfWork.BeginTransactionAsync();
        Context.Currencies.Add(new CurrencyEntity
        {
            Code = Core.ValueObjects.Currency.Create(value: "INN").Value,
            Name = "Inner",
            Symbol = "I",
            IsActive = true
        });

        await Context.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        await _unitOfWork.CommitAsync();

        int outCount = await Context.Currencies.CountAsync(c => c.Code == "OUT");
        int innCount = await Context.Currencies.CountAsync(c => c.Code == "INN");

        await Assert.That(value: outCount).IsEqualTo(expected: 1);
        await Assert.That(value: innCount).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task RollbackWithoutTransaction_ShouldNotThrow()
    {
        await Assert.That(
            action: async () => await _unitOfWork.RollbackAsync()
        ).ThrowsNothing();
    }

    [Test]
    public async Task CommitWithoutTransaction_ShouldThrowInvalidOperationException()
    {
        await Assert.That(
            action: async () => await _unitOfWork.CommitAsync()
        ).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ExecuteInTransactionAsync_WhenOperationSucceeds_ShouldPersistChanges()
    {
        await _unitOfWork.ExecuteInTransactionAsync(operation: async () =>
        {
            Context.Currencies.Add(new CurrencyEntity
            {
                Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
                Name = "Test",
                Symbol = "T",
                IsActive = true
            });
            await Context.SaveChangesAsync();
        });

        int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task ExecuteInTransactionAsync_WhenOperationThrows_ShouldRollbackAndRethrow()
    {
        await Assert.That(action: async () =>
        {
            await _unitOfWork.ExecuteInTransactionAsync(operation: async () =>
            {
                Context.Currencies.Add(new CurrencyEntity
                {
                    Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
                    Name = "Test",
                    Symbol = "T",
                    IsActive = true
                });
                await Context.SaveChangesAsync();
                throw new InvalidOperationException("Simulated failure");
            });
        }).Throws<InvalidOperationException>();

        int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task ExecuteInTransactionAsync_WithOnError_WhenOperationThrows_ShouldCallOnErrorAndRethrow()
    {
        bool onErrorCalled = false;

        await Assert.That(action: async () =>
        {
            await _unitOfWork.ExecuteInTransactionAsync(
                operation: async () =>
                {
                    Context.Currencies.Add(new CurrencyEntity
                    {
                        Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
                        Name = "Test",
                        Symbol = "T",
                        IsActive = true
                    });
                    await Context.SaveChangesAsync();
                    throw new InvalidOperationException("Simulated failure");
                },
                onError: _ =>
                {
                    onErrorCalled = true;
                    return Task.CompletedTask;
                }
            );
        }).Throws<InvalidOperationException>();

        int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 0);
        await Assert.That(value: onErrorCalled).IsTrue();
    }

    [Test]
    public async Task ExecuteInTransactionAsync_WithOnError_WhenOperationSucceeds_ShouldNotCallOnError()
    {
        bool onErrorCalled = false;

        await _unitOfWork.ExecuteInTransactionAsync(
            operation: async () =>
            {
                Context.Currencies.Add(new CurrencyEntity
                {
                    Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
                    Name = "Test",
                    Symbol = "T",
                    IsActive = true
                });
                await Context.SaveChangesAsync();
            },
            onError: _ =>
            {
                onErrorCalled = true;
                return Task.CompletedTask;
            }
        );

        await Assert.That(value: onErrorCalled).IsFalse();
        int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task GenericExecuteInTransactionAsync_WhenOperationSucceeds_ShouldReturnValueAndPersistChanges()
    {
        Currency code = Currency.Create(value: "TST").Value;

        string returned = await _unitOfWork.ExecuteInTransactionAsync(operation: async () =>
        {
            Context.Currencies.Add(entity: new CurrencyEntity
            {
                Code = code,
                Name = "Test",
                Symbol = "T",
                IsActive = true
            });
            await Context.SaveChangesAsync();
            return "ok";
        });

        await Assert.That(value: returned).IsEqualTo(expected: "ok");
        int count = await Context.Currencies.CountAsync(predicate: c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task GenericExecuteInTransactionAsync_WhenOperationThrows_ShouldRollbackAndRethrow()
    {
        await Assert.That(action: async () => await _unitOfWork.ExecuteInTransactionAsync(operation: async () =>
        {
            Context.Currencies.Add(new CurrencyEntity
            {
                Code = Currency.Create(value: "TST").Value,
                Name = "Test",
                Symbol = "T",
                IsActive = true
            });
            await Context.SaveChangesAsync();
            throw new InvalidOperationException("Simulated failure");
        })).Throws<InvalidOperationException>();

        int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task GenericExecuteInTransactionAsync_WithOnError_WhenOperationThrows_ShouldCallOnErrorAndRethrow()
    {
        bool onErrorCalled = false;

        await Assert.That(action: async () => await _unitOfWork.ExecuteInTransactionAsync(
            operation: async () =>
            {
                Context.Currencies.Add(new CurrencyEntity
                {
                    Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
                    Name = "Test",
                    Symbol = "T",
                    IsActive = true
                });
                await Context.SaveChangesAsync();
                throw new InvalidOperationException("Simulated failure");
            },
            onError: _ =>
            {
                onErrorCalled = true;
                return Task.CompletedTask;
            }
        )).Throws<InvalidOperationException>();

        int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
        await Assert.That(value: count).IsEqualTo(expected: 0);
        await Assert.That(value: onErrorCalled).IsTrue();
    }

    [Test]
    public async Task GenericExecuteInTransactionAsync_WithOnError_WhenOperationSucceeds_ShouldNotCallOnError()
    {
        bool onErrorCalled = false;

        string returned = await _unitOfWork.ExecuteInTransactionAsync(
            operation: async () =>
            {
                Context.Currencies.Add(new CurrencyEntity
                {
                    Code = Core.ValueObjects.Currency.Create(value: "TST").Value,
                    Name = "Test",
                    Symbol = "T",
                    IsActive = true
                });
                await Context.SaveChangesAsync();
                return "ok";
            },
            onError: _ =>
            {
                onErrorCalled = true;
                return Task.CompletedTask;
            }
        );

        await Assert.That(value: returned).IsEqualTo(expected: "ok");
        await Assert.That(value: onErrorCalled).IsFalse();
    }
}