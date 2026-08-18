using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.ReadModels.Operation;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Endpoints.Users.Contracts;

public sealed record OperationTransactionDetails(
	Guid? AccountId,
	Guid CategoryId,
	decimal Amount,
	Currency? Currency,
	DirectionType Direction,
	bool IsExcluded)
{
	public static OperationTransactionDetails FromReadModel(TransactionDetails readModel) => new OperationTransactionDetails(
		AccountId: readModel.AccountId,
		CategoryId: readModel.CategoryId,
		Amount: readModel.Amount,
		Currency: readModel.Currency,
		Direction: readModel.Direction,
		IsExcluded: readModel.IsExcluded
	);
}

public sealed record OperationTransferDetails(
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	Currency CurrencyFrom,
	decimal AmountTo,
	Currency CurrencyTo,
	TransferStatus Status)
{
	public static OperationTransferDetails FromReadModel(TransferDetails readModel) => new OperationTransferDetails(
		FromAccountId: readModel.FromAccountId,
		ToAccountId: readModel.ToAccountId,
		AmountFrom: readModel.AmountFrom,
		CurrencyFrom: readModel.CurrencyFrom,
		AmountTo: readModel.AmountTo,
		CurrencyTo: readModel.CurrencyTo,
		Status: readModel.Status
	);
}

/// <summary>
/// HTTP projection of <see cref="Operation"/>
/// </summary>
public sealed record OperationResponse(
	Guid Id,
	OperationFilterType Type,
	string? Description,
	DateTimeOffset OccurredAt,
	OperationTransactionDetails? Transaction,
	OperationTransferDetails? Transfer
) : IResponseOf<Operation, OperationResponse>
{
	public static OperationResponse FromReadModel(Operation readModel) => new OperationResponse(
		Id: readModel.Id,
		Type: readModel.Type,
		Description: readModel.Description,
		OccurredAt: readModel.OccurredAt,
		Transaction: readModel.Transaction is null ? null : OperationTransactionDetails.FromReadModel(readModel: readModel.Transaction),
		Transfer: readModel.Transfer is null ? null : OperationTransferDetails.FromReadModel(readModel: readModel.Transfer)
	);
}
