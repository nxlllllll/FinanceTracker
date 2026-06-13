using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

internal static class HistoryRowMapper
{
	// Column ordinals matching the SELECT in HistoryQuery:
	// 0  Id            uuid
	// 1  Type          text  ('Transaction' | 'Transfer')
	// 2  Description   text?
	// 3  OccurredAt    timestamptz
	// 4  AccountId     uuid?
	// 5  CategoryId    uuid?
	// 6  Amount        numeric?
	// 7  CurrencyCode  text?
	// 8  Direction     text?
	// 9  IsExcluded    bool?
	// 10 FromAccountId uuid?
	// 11 ToAccountId   uuid?
	// 12 AmountFrom    numeric?
	// 13 CurrencyFrom  text?
	// 14 AmountTo      numeric?
	// 15 CurrencyTo    text?

	public static Operation MapFromReader(NpgsqlDataReader reader)
	{
		bool isTransaction = reader.GetString(ordinal: 1) == "Transaction";

		DirectionType? direction = null;
		if (!reader.IsDBNull(ordinal: 8))
			direction = Enum.Parse<DirectionType>(value: reader.GetString(ordinal: 8), ignoreCase: true);

		OperationFilterType type = isTransaction
			? direction == DirectionType.Credit ? OperationFilterType.Income : OperationFilterType.Expense
			: OperationFilterType.Transfer;

		return new Operation(
			Id: reader.GetGuid(ordinal: 0),
			Type: type,
			Description: reader.IsDBNull(ordinal: 2) ? null : reader.GetString(ordinal: 2),
			OccurredAt: reader.GetFieldValue<DateTimeOffset>(ordinal: 3),
			Transaction: isTransaction ? new TransactionDetails(
				AccountId: reader.GetGuid(ordinal: 4),
				CategoryId: reader.GetGuid(ordinal: 5),
				Amount: reader.GetDecimal(ordinal: 6),
				Currency: Core.ValueObjects.Currency.Reconstitute(value: reader.GetString(ordinal: 7)),
				Direction: direction!.Value,
				IsExcluded: reader.GetBoolean(ordinal: 9)
			) : null,
			Transfer: !isTransaction ? new TransferDetails(
				FromAccountId: reader.GetGuid(ordinal: 10),
				ToAccountId: reader.GetGuid(ordinal: 11),
				AmountFrom: reader.GetDecimal(ordinal: 12),
				CurrencyFrom: Core.ValueObjects.Currency.Reconstitute(value: reader.GetString(ordinal: 13)),
				AmountTo: reader.GetDecimal(ordinal: 14),
				CurrencyTo: Core.ValueObjects.Currency.Reconstitute(value: reader.GetString(ordinal: 15))
			) : null
		);
	}
}