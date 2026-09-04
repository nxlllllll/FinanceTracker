using FinanceTracker.Api.Endpoints.Transfers.Queries;
using FinanceTracker.Api.Http;
using FinanceTracker.Application.UseCases.Transfer.Queries.GetTransfer;
using FinanceTracker.Application.UseCases.Transfer.Queries.GetTransfers;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.Results;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class TransferQueryEndpointTests
{
	private static readonly Guid CallerId = Guid.CreateVersion7();

	private static ICurrentUserProvider CurrentUser()
	{
		ICurrentUserProvider currentUser = Substitute.For<ICurrentUserProvider>();
		currentUser.UserId.Returns(returnThis: CallerId);

		return currentUser;
	}

	[Test]
	public async Task GetTransfer_ShouldReadTheCallersOwnTransfer()
	{
		Guid transferId = Guid.CreateVersion7();

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetTransferQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<TransferReadModel, AppException>.Failure(
			error: new NotFoundException(message: "Transfer not found.", id: transferId)
		));

		await GetTransferEndpoint.HandleAsync(
			transferId: transferId,
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTransferQuery>(predicate: query => query!.TransferId == transferId && query.UserId == CallerId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetTransfers_ShouldPassTheFiltersThroughAndScopeThemToTheCaller()
	{
		Guid accountId = Guid.CreateVersion7();
		Guid cursorId = Guid.CreateVersion7();
		DateTimeOffset dateFrom = new DateTimeOffset(year: 2026, month: 3, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.FromHours(value: 3));

		ISender sender = Substitute.For<ISender>();
		sender.Send(
			request: Arg.Any<GetTransfersQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<PagedResult<TransferReadModel>, AppException>.Success(value: new PagedResult<TransferReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		)));

		await GetTransfersEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			accountId: accountId,
			status: "pendingCredit",
			dateFrom: dateFrom,
			cursorOccurredAt: dateFrom,
			cursorId: cursorId,
			pageSize: 50
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<GetTransfersQuery>(predicate: query =>
				query!.UserId == CallerId &&
				query.AccountId == accountId &&
				query.Status == TransferStatus.PendingCredit &&
				query.DateFrom == dateFrom.ToUniversalTime() &&
				query.CursorId == cursorId &&
				query.PageSize == 50
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetTransfers_WithAStatusItCannotParse_ShouldNotReachTheHandler()
	{
		ISender sender = Substitute.For<ISender>();

		await GetTransfersEndpoint.HandleAsync(
			currentUser: CurrentUser(),
			sender: sender,
			ct: CancellationToken.None,
			status: "halfway"
		);

		await sender.DidNotReceive().Send(
			request: Arg.Any<GetTransfersQuery>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
