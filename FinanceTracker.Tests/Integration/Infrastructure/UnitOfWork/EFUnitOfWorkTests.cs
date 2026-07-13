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
			Code = Currency.Create(value: "TST").Value,
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
			Code = Currency.Create(value: "TST").Value,
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
			Code = Currency.Create(value: "OUT").Value,
			Name = "Output",
			Symbol = "O",
			IsActive = true
		});

		await Context.SaveChangesAsync();

		await _unitOfWork.BeginTransactionAsync();
		Context.Currencies.Add(new CurrencyEntity
		{
			Code = Currency.Create(value: "TST").Value,
			Name = "Test",
			Symbol = "T",
			IsActive = true
		});
		await Context.SaveChangesAsync();
		await _unitOfWork.RollbackAsync();

		await _unitOfWork.CommitAsync();

		int outCount = await Context.Currencies.CountAsync(predicate: c => c.Code == "OUT");
		int testCount = await Context.Currencies.CountAsync(predicate: c => c.Code == "TST");

		await Assert.That(value: outCount).IsEqualTo(expected: 1);
		await Assert.That(value: testCount).IsEqualTo(expected: 0)
			.Because(message: "The savepoint rollback should discard the nested transaction's insert at the database level.");
	}

	[Test]
	public async Task NestedBeginAndRollback_ShouldNotLeaveTheRolledBackEntityTrackedAsUnchanged()
	{
		await _unitOfWork.BeginTransactionAsync();

		await _unitOfWork.BeginTransactionAsync();
		Context.Currencies.Add(new CurrencyEntity
		{
			Code = Currency.Create(value: "TST").Value,
			Name = "Test",
			Symbol = "T",
			IsActive = true
		});
		await Context.SaveChangesAsync();
		await _unitOfWork.RollbackAsync();

		bool stillTracked = Context.ChangeTracker.Entries<CurrencyEntity>()
			.Any(predicate: e => e.Entity.Code == "TST" && e.State != EntityState.Detached);

		await Assert.That(value: stillTracked).IsFalse().Because(message: """
		After a savepoint rollback, the entity the rolled-back nested transaction inserted must not remain tracked as if it still exists — the database no longer has it.
		""");

		await _unitOfWork.RollbackAsync();
	}

	[Test]
	public async Task NestedBeginAndCommit_ShouldPersistBothLevels()
	{
		await _unitOfWork.BeginTransactionAsync();

		Context.Currencies.Add(new CurrencyEntity
		{
			Code = Currency.Create(value: "OUT").Value,
			Name = "Output",
			Symbol = "O",
			IsActive = true
		});
		await Context.SaveChangesAsync();

		await _unitOfWork.BeginTransactionAsync();
		Context.Currencies.Add(new CurrencyEntity
		{
			Code = Currency.Create(value: "INN").Value,
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
		=> await Assert.That(action: async () => await _unitOfWork.RollbackAsync()).ThrowsNothing();

	[Test]
	public async Task CommitWithoutTransaction_ShouldThrowInvalidOperationException()
		=> await Assert.That(action: async () => await _unitOfWork.CommitAsync()).Throws<InvalidOperationException>();

	[Test]
	public async Task ExecuteInTransactionAsync_WhenOperationSucceeds_ShouldPersistChanges()
	{
		await _unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			Context.Currencies.Add(new CurrencyEntity
			{
				Code = Currency.Create(value: "TST").Value,
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
	public async Task ExecuteInTransactionAsync_WithOnError_WhenOperationThrows_ShouldCallOnErrorAndRethrow()
	{
		bool onErrorCalled = false;

		await Assert.That(action: async () => await _unitOfWork.ExecuteInTransactionAsync(
			operation: async () =>
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
	public async Task ExecuteInTransactionAsync_WithOnError_WhenOperationSucceeds_ShouldNotCallOnError()
	{
		bool onErrorCalled = false;

		await _unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			Context.Currencies.Add(new CurrencyEntity
			{
				Code = Currency.Create(value: "TST").Value,
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
		});

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
		},
		onError: _ =>
		{
			onErrorCalled = true;
			return Task.CompletedTask;
		})).Throws<InvalidOperationException>();

		int count = await Context.Currencies.CountAsync(c => c.Code == "TST");
		await Assert.That(value: count).IsEqualTo(expected: 0);
		await Assert.That(value: onErrorCalled).IsTrue();
	}

	[Test]
	public async Task GenericExecuteInTransactionAsync_WithOnError_WhenOperationSucceeds_ShouldNotCallOnError()
	{
		bool onErrorCalled = false;

		string returned = await _unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			Context.Currencies.Add(new CurrencyEntity
			{
				Code = Currency.Create(value: "TST").Value,
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
		});

		await Assert.That(value: returned).IsEqualTo(expected: "ok");
		await Assert.That(value: onErrorCalled).IsFalse();
	}

	[Test]
	public async Task OnCommitted_WhenTransactionCommits_ShouldRunCallback()
	{
		bool called = false;

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => called = true);
		await _unitOfWork.CommitAsync();

		await Assert.That(value: called).IsTrue();
	}

	[Test]
	public async Task OnCommitted_WhenTransactionRollsBack_ShouldNotRunCallback()
	{
		bool called = false;

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => called = true);
		await _unitOfWork.RollbackAsync();

		await Assert.That(value: called).IsFalse();
	}

	[Test]
	public async Task OnCommitted_RegisteredBeforeNestedSavepoint_WhenNestedSavepointRollsBack_ShouldStillRunOnOuterCommit()
	{
		bool outerCallbackCalled = false;

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => outerCallbackCalled = true);

		await _unitOfWork.BeginTransactionAsync();
		Context.Currencies.Add(new CurrencyEntity
		{
			Code = Currency.Create(value: "TST").Value,
			Name = "Test",
			Symbol = "T",
			IsActive = true
		});
		await Context.SaveChangesAsync();
		await _unitOfWork.RollbackAsync();

		await _unitOfWork.CommitAsync();

		await Assert.That(value: outerCallbackCalled).IsTrue().Because(message: """
			A callback registered before a nested savepoint describes work that already belongs to the
			outer scope. Rolling back the nested savepoint must not discard it — it must still run once
			the outer transaction, which is still committing successfully, actually commits.
		""");
	}

	[Test]
	public async Task OnCommitted_RegisteredInsideSavepoint_WhenSavepointRollsBack_ShouldNotRun()
	{
		bool innerCallbackCalled = false;

		await _unitOfWork.BeginTransactionAsync();

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => innerCallbackCalled = true);
		await _unitOfWork.RollbackAsync();

		await _unitOfWork.CommitAsync();

		await Assert.That(value: innerCallbackCalled).IsFalse().Because(message: """
			A callback registered inside a savepoint describes work that was just rolled back — it must
			be discarded along with that work, not survive to the outer commit.
		""");
	}

	[Test]
	public async Task OnCommitted_RegisteredInsideSavepoint_WhenSavepointCommitsButOuterRollsBack_ShouldNotRun()
	{
		bool innerCallbackCalled = false;

		await _unitOfWork.BeginTransactionAsync();

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => innerCallbackCalled = true);
		await _unitOfWork.CommitAsync();

		await _unitOfWork.RollbackAsync();

		await Assert.That(value: innerCallbackCalled).IsFalse().Because(message: """
			Releasing a savepoint is not a durable commit. A callback that graduated to the outer scope
			must still be discarded if the outer transaction itself subsequently rolls back.
		""");
	}

	[Test]
	public async Task OnCommitted_MultipleCallbacksInSameSavepointScope_WhenSavepointRollsBack_ShouldDiscardBoth()
	{
		bool firstCalled = false;
		bool secondCalled = false;

		await _unitOfWork.BeginTransactionAsync();

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => firstCalled = true);
		_unitOfWork.OnCommitted(callback: () => secondCalled = true);
		await _unitOfWork.RollbackAsync();

		await _unitOfWork.CommitAsync();

		await Assert.That(value: firstCalled).IsFalse();
		await Assert.That(value: secondCalled).IsFalse();
	}

	[Test]
	public async Task OnCommitted_MultipleCallbacksAcrossScopes_WhenOuterCommits_ShouldRunInRegistrationOrder()
	{
		List<int> executionOrder = [];

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => executionOrder.Add(item: 1));

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => executionOrder.Add(item: 2));
		_unitOfWork.OnCommitted(callback: () => executionOrder.Add(item: 3));
		await _unitOfWork.CommitAsync();

		_unitOfWork.OnCommitted(callback: () => executionOrder.Add(item: 4));
		await _unitOfWork.CommitAsync();

		await Assert.That(value: executionOrder.Count).IsEqualTo(expected: 4);
		await Assert.That(value: executionOrder[0]).IsEqualTo(expected: 1);
		await Assert.That(value: executionOrder[1]).IsEqualTo(expected: 2);
		await Assert.That(value: executionOrder[2]).IsEqualTo(expected: 3);
		await Assert.That(value: executionOrder[3]).IsEqualTo(expected: 4);
	}

	[Test]
	public async Task OnCommitted_WhenOneCallbackThrows_ShouldStillRunRemainingCallbacksAndNotRethrow()
	{
		bool secondCalled = false;

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => throw new InvalidOperationException(message: "First callback failed"));
		_unitOfWork.OnCommitted(callback: () => secondCalled = true);

		await _unitOfWork.CommitAsync();

		await Assert.That(value: secondCalled).IsTrue().Because(message: """
			The transaction has already committed successfully by the time OnCommitted callbacks run, so
			one callback throwing must not prevent an unrelated callback from still doing its job.
		""");
	}

	[Test]
	public async Task OnCommitted_WhenOneCallbackThrows_ShouldNotRethrowToCaller()
	{
		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => throw new InvalidOperationException(message: "Callback failed"));

		await Assert.That(
			action: async () => await _unitOfWork.CommitAsync()
		).ThrowsNothing().Because(message: """
			By the time OnCommitted callbacks run, the transaction is already durably committed —
			a callback failure must never be mistaken for the operation itself failing (e.g. it must
			not cause an idempotent command handler to release its key and re-execute on retry).
			The failure is still logged and counted via FinanceTrackerMetrics.OnCommittedCallbackFailures.
		""");
	}

	[Test]
	public async Task OnCommitted_WhenMultipleCallbacksThrow_ShouldStillRunAllAndNotRethrow()
	{
		bool thirdCalled = false;

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => throw new InvalidOperationException(message: "First callback failed"));
		_unitOfWork.OnCommitted(callback: () => throw new InvalidOperationException(message: "Second callback failed"));
		_unitOfWork.OnCommitted(callback: () => thirdCalled = true);

		await _unitOfWork.CommitAsync();

		await Assert.That(value: thirdCalled).IsTrue();
	}

	[Test]
	public async Task OnCommittedAsync_WhenTransactionCommits_ShouldAwaitCallback()
	{
		bool called = false;

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: async () =>
		{
			await Task.Yield();
			called = true;
		});
		await _unitOfWork.CommitAsync();

		await Assert.That(value: called).IsTrue().Because(message: """
			The async overload exists specifically so callers (e.g. a MediatR Publish call) can
			run real awaited work after commit — a fire-and-forget wrapper would make this
			unreliable, since the test would frequently observe `called` still false.
		""");
	}

	[Test]
	public async Task OnCommittedAsync_WhenTransactionRollsBack_ShouldNotRunCallback()
	{
		bool called = false;

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: async () =>
		{
			await Task.Yield();
			called = true;
		});
		await _unitOfWork.RollbackAsync();

		await Assert.That(value: called).IsFalse();
	}

	[Test]
	public async Task OnCommittedAsync_RegisteredInsideSavepoint_WhenSavepointRollsBack_ShouldNotRun()
	{
		bool innerCallbackCalled = false;

		await _unitOfWork.BeginTransactionAsync();

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: async () =>
		{
			await Task.Yield();
			innerCallbackCalled = true;
		});
		await _unitOfWork.RollbackAsync();

		await _unitOfWork.CommitAsync();

		await Assert.That(value: innerCallbackCalled).IsFalse();
	}

	[Test]
	public async Task OnCommittedAsync_AndSyncCallbacksInSameScope_ShouldBothRunInRegistrationOrder()
	{
		List<int> executionOrder = [];

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: () => executionOrder.Add(item: 1));
		_unitOfWork.OnCommitted(callback: async () =>
		{
			await Task.Yield();
			executionOrder.Add(item: 2);
		});
		_unitOfWork.OnCommitted(callback: () => executionOrder.Add(item: 3));

		await _unitOfWork.CommitAsync();

		await Assert.That(value: executionOrder.Count).IsEqualTo(expected: 3);
		await Assert.That(value: executionOrder[0]).IsEqualTo(expected: 1);
		await Assert.That(value: executionOrder[1]).IsEqualTo(expected: 2);
		await Assert.That(value: executionOrder[2]).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task OnCommittedAsync_WhenCallbackThrows_ShouldStillRunRemainingCallbacksAndNotRethrow()
	{
		bool secondCalled = false;

		await _unitOfWork.BeginTransactionAsync();
		_unitOfWork.OnCommitted(callback: async () =>
		{
			await Task.Yield();
			throw new InvalidOperationException(message: "Async callback failed");
		});
		_unitOfWork.OnCommitted(callback: () => secondCalled = true);

		await Assert.That(
			action: async () => await _unitOfWork.CommitAsync()
		).ThrowsNothing().Because(message: """
			Same guarantee as the sync overload: a failing OnCommitted callback runs strictly
			after the real commit, so it must never surface as a failure of the commit itself.
		""");
		await Assert.That(value: secondCalled).IsTrue();
	}
}
