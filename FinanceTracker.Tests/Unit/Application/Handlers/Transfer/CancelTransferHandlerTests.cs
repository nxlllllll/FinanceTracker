using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Transfer.Commands.CancelTransfer;
using FinanceTracker.Application.UseCases.Transfer.Notifications;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transfer;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AccountAggregate = FinanceTracker.Core.Domains.Account.Account;
using TransferAggregate = FinanceTracker.Core.Domains.Transfer.Transfer;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transfer;

public sealed class CancelTransferHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private ITransferWriteRepository _transferWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private CancelTransferHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_transferWriteRepository = Substitute.For<ITransferWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());

		_handler = new CancelTransferHandler(
			accountRepository: _accountRepository,
			transferWriteRepository: _transferWriteRepository,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default,
			options: new FakeOptionsMonitor<CancellationOptions>(new CancellationOptions()),
			logger: Substitute.For<ILogger<CancelTransferHandler>>()
		);
	}

	private (TransferAggregate Transfer, AccountAggregate From, AccountAggregate To) Arrange(
		TransferStatus status,
		decimal fromBalance = 10_000m,
		decimal toBalance = 10_000m,
		decimal amount = 1_000m)
	{
		AccountAggregate from = AccountFactory.Create(balance: fromBalance).Value!;
		AccountAggregate to = AccountFactory.Create(userId: from.UserId, balance: toBalance).Value!;

		TransferAggregate transfer = TransferFactory.Reconstitute(
			userId: from.UserId,
			fromAccountId: from.Id,
			toAccountId: to.Id,
			amount: amount,
			status: status
		);

		_accountRepository.GetByIdAsync(accountId: from.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: from);
		_accountRepository.GetByIdAsync(accountId: to.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: to);

		return (transfer, from, to);
	}

	private Task<Result<Guid, AppException>> CancelAsync(TransferAggregate transfer) => _handler.HandleAsync(
		command: new CancelTransferCommand(UserId: transfer.UserId, TransferId: transfer.Id),
		transfer: transfer,
		ct: CancellationToken.None
	);

	[Test]
	public async Task HandleAsync_WhileTheCreditIsStillPending_ShouldOnlyTouchTheSource()
	{
		(TransferAggregate transfer, AccountAggregate from, AccountAggregate to) = Arrange(status: TransferStatus.PendingCredit);

		Result<Guid, AppException> result = await CancelAsync(transfer: transfer);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: from.Balance.Amount).IsEqualTo(expected: 11_000m);
		await Assert.That(value: to.Balance.Amount).IsEqualTo(expected: 10_000m).Because(message: """
			Nothing ever reached the destination while the credit was pending. Taking money off it here
			would invent a debit for a movement that never landed.
		""");

		await _accountRepository.DidNotReceive().SaveAsync(account: to, ct: Arg.Any<CancellationToken>());

		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).SaveStatusAsync(transfer: transfer, ct: Arg.Any<CancellationToken>());
		await _transferWriteRepository.DidNotReceive().CancelAsync(
			transfer: Arg.Any<TransferAggregate>(),
			reversalId: Arg.Any<Guid>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_AfterTheTransferCompleted_ShouldUnwindBothSides()
	{
		(TransferAggregate transfer, AccountAggregate from, AccountAggregate to) = Arrange(status: TransferStatus.Completed);

		Result<Guid, AppException> result = await CancelAsync(transfer: transfer);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: from.Balance.Amount).IsEqualTo(expected: 11_000m);
		await Assert.That(value: to.Balance.Amount).IsEqualTo(expected: 9_000m);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(account: from, ct: Arg.Any<CancellationToken>());
		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(account: to, ct: Arg.Any<CancellationToken>());

		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).CancelAsync(
			transfer: transfer,
			reversalId: Arg.Any<Guid>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _transferWriteRepository.DidNotReceive().SaveStatusAsync(transfer: Arg.Any<TransferAggregate>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WhenTheDestinationCannotGiveTheMoneyBack_ShouldUndoNothing()
	{
		(TransferAggregate transfer, AccountAggregate from, AccountAggregate to) = Arrange(
			status: TransferStatus.Completed,
			toBalance: 100m
		);

		Result<Guid, AppException> result = await CancelAsync(transfer: transfer);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InsufficientFundsException>();

		await Assert.That(value: from.Balance.Amount).IsEqualTo(expected: 10_000m).Because(message: """
			The destination is asked before the source is given anything back, so a refusal leaves both
			accounts untouched. Reversing the source first would hand the money over twice and leave the
			transfer half undone with no way to finish it.
		""");

		await _accountRepository.DidNotReceive().SaveAsync(account: Arg.Any<AccountAggregate>(), ct: Arg.Any<CancellationToken>());
		await _transferWriteRepository.DidNotReceive().CancelAsync(
			transfer: Arg.Any<TransferAggregate>(),
			reversalId: Arg.Any<Guid>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	[Arguments(TransferStatus.Compensated)]
	[Arguments(TransferStatus.Failed)]
	[Arguments(TransferStatus.Cancelled)]
	public async Task HandleAsync_WhenThereIsNothingLeftToUndo_ShouldRefuseBeforeTouchingAnyAccount(TransferStatus status)
	{
		(TransferAggregate transfer, AccountAggregate from, AccountAggregate to) = Arrange(status: status);

		Result<Guid, AppException> result = await CancelAsync(transfer: transfer);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransferStatusException>();
		await Assert.That(value: from.Balance.Amount).IsEqualTo(expected: 10_000m);
		await Assert.That(value: to.Balance.Amount).IsEqualTo(expected: 10_000m);
	}

	[Test]
	public async Task HandleAsync_PastTheCancellationWindow_ShouldRefuse()
	{
		AccountAggregate from = AccountFactory.Create(balance: 10_000m).Value!;
		AccountAggregate to = AccountFactory.Create(userId: from.UserId, balance: 10_000m).Value!;

		TransferAggregate transfer = TransferFactory.Reconstitute(
			userId: from.UserId,
			fromAccountId: from.Id,
			toAccountId: to.Id,
			status: TransferStatus.Completed,
			occurredAt: FakeDateProvider.Default.UtcNow.AddDays(days: -31)
		);

		_accountRepository.GetByIdAsync(accountId: from.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: from);

		Result<Guid, AppException> result = await CancelAsync(transfer: transfer);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<TransferCancellationWindowExpiredException>();
	}

	[Test]
	public async Task HandleAsync_ShouldStageTheAuditNotificationOnlyAfterTheCommit()
	{
		(TransferAggregate transfer, AccountAggregate _, AccountAggregate _) = Arrange(status: TransferStatus.Completed);

		await CancelAsync(transfer: transfer);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<TransferCancelledNotification>(
			predicate: n => n.TransferId == transfer.Id && n.CreditHadLanded
		));
	}

	[Test]
	public async Task HandleAsync_WhenTheSourceAccountIsGone_ShouldRefuse()
	{
		(TransferAggregate transfer, AccountAggregate from, AccountAggregate _) = Arrange(status: TransferStatus.PendingCredit);

		_accountRepository.GetByIdAsync(accountId: from.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: (AccountAggregate?)null);

		Result<Guid, AppException> result = await CancelAsync(transfer: transfer);

		await Assert.That(value: result.IsFailure).IsTrue();
	}
}
