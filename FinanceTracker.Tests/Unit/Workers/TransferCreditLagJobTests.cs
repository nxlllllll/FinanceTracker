using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.TransferCompensation;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.TransferProjection.Job;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class TransferCreditLagJobTests
{
	private ITransferReadRepository _transferReadRepository = null!;
	private ITransferCompensationService _compensationService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IJobExecutionContext _jobContext = null!;
	private TransferCreditLagJob _job = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transferReadRepository = Substitute.For<ITransferReadRepository>();
		_compensationService = Substitute.For<ITransferCompensationService>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_jobContext = Substitute.For<IJobExecutionContext>();
		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task>>()?.Invoke());

		_transferReadRepository.GetPendingCreditCountAsync(
			gracePeriod: Arg.Any<TimeSpan>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);

		SetupStuckTransfers(transfers: []);

		_job = new TransferCreditLagJob(
			transferReadRepository: _transferReadRepository,
			compensationService: _compensationService,
			options: new FakeOptionsMonitor<TransferCreditLagOptions>(value: new TransferCreditLagOptions()),
			unitOfWork: _unitOfWork,
			logger: Substitute.For<ILogger<TransferCreditLagJob>>()
		);
	}

	private void SetupStuckTransfers(IReadOnlyList<PendingCreditTransfer> transfers)
	{
		_transferReadRepository.GetPendingCreditForCompensationAsync(
			compensationThreshold: Arg.Any<TimeSpan>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transfers);
	}

	private static PendingCreditTransfer CreateStuckTransfer() => new PendingCreditTransfer(
		TransferId: Guid.CreateVersion7(),
		FromAccountId: Guid.CreateVersion7(),
		Amount: 1000m,
		OccurredAt: DateTimeOffset.UtcNow.AddHours(hours: -1)
	);

	[Test]
	public async Task Execute_WhenNoStuckTransfers_ShouldNotCallCompensate()
	{
		await _job.Execute(context: _jobContext);

		await _compensationService.DidNotReceive().CompensateAsync(
			transfer: Arg.Any<PendingCreditTransfer>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenStuckTransfersExist_ShouldCompensateEach()
	{
		SetupStuckTransfers(transfers: [CreateStuckTransfer(), CreateStuckTransfer(), CreateStuckTransfer()]);

		await _job.Execute(context: _jobContext);

		await _compensationService.Received(requiredNumberOfCalls: 3).CompensateAsync(
			transfer: Arg.Any<PendingCreditTransfer>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenCompensateThrowsConcurrencyConflictOnSecond_ShouldStillCompensateFirstAndThird()
	{
		PendingCreditTransfer first = CreateStuckTransfer();
		PendingCreditTransfer second = CreateStuckTransfer();
		PendingCreditTransfer third = CreateStuckTransfer();

		SetupStuckTransfers(transfers: [first, second, third]);

		_compensationService.CompensateAsync(
			transfer: second,
			ct: Arg.Any<CancellationToken>()
		).Throws(createException: _ => new ConcurrencyConflictException(message: "Conflict.", id: second.TransferId));

		await _job.Execute(context: _jobContext);

		await _compensationService.Received(requiredNumberOfCalls: 1).CompensateAsync(transfer: first, ct: Arg.Any<CancellationToken>());
		await _compensationService.Received(requiredNumberOfCalls: 1).CompensateAsync(transfer: second, ct: Arg.Any<CancellationToken>());
		await _compensationService.Received(requiredNumberOfCalls: 1).CompensateAsync(transfer: third, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Execute_WhenCompensateThrowsUnexpectedExceptionOnSecond_ShouldStillCompensateFirstAndThird()
	{
		PendingCreditTransfer first = CreateStuckTransfer();
		PendingCreditTransfer second = CreateStuckTransfer();
		PendingCreditTransfer third = CreateStuckTransfer();

		SetupStuckTransfers(transfers: [first, second, third]);

		_compensationService.CompensateAsync(
			transfer: second,
			ct: Arg.Any<CancellationToken>()
		).Throws(createException: _ => new InvalidOperationException(message: "Database connection lost."));

		await _job.Execute(context: _jobContext);

		await _compensationService.Received(requiredNumberOfCalls: 1).CompensateAsync(transfer: first, ct: Arg.Any<CancellationToken>());
		await _compensationService.Received(requiredNumberOfCalls: 1).CompensateAsync(transfer: third, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Execute_WhenCompensateThrows_ShouldNotPropagateExceptionToCaller()
	{
		SetupStuckTransfers(transfers: [CreateStuckTransfer()]);

		_compensationService.CompensateAsync(
			transfer: Arg.Any<PendingCreditTransfer>(),
			ct: Arg.Any<CancellationToken>()
		).Throws(createException: _ => new InvalidOperationException(message: "Database connection lost."));

		await _job.Execute(context: _jobContext);
	}
}
