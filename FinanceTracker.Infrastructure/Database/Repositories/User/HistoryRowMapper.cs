using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

internal static class HistoryRowMapper
{
	public static Operation Map(HistoryRow row)
	{
		bool isTransaction = row.Type == "Transaction";
		
		DirectionType? direction = row.Direction is not null
			? Enum.Parse<DirectionType>(value: row.Direction, ignoreCase: true)
			: null;

		OperationFilterType type = isTransaction
			? direction == DirectionType.Credit ? OperationFilterType.Income : OperationFilterType.Expense
			: OperationFilterType.Transfer;

		return new Operation(
			Id: row.Id,
			Type: type,
			Description: row.Description,
			OccurredAt: row.OccurredAt,
			Transaction: isTransaction ? new TransactionDetails(
				AccountId: row.AccountId,
				CategoryId: row.CategoryId!.Value,
				Amount: row.Amount!.Value,
				Currency: Core.ValueObjects.Currency.Reconstitute(value: row.CurrencyCode!),
				Direction: direction!.Value,
				IsExcluded: row.IsExcluded!.Value
			) : null,
			Transfer: !isTransaction ? new TransferDetails(
				FromAccountId: row.FromAccountId!.Value,
				ToAccountId: row.ToAccountId!.Value,
				AmountFrom: row.AmountFrom!.Value,
				CurrencyFrom: Core.ValueObjects.Currency.Reconstitute(value: row.CurrencyFrom!),
				AmountTo: row.AmountTo!.Value,
				CurrencyTo: Core.ValueObjects.Currency.Reconstitute(value: row.CurrencyTo!)
			) : null
		);
	}
}