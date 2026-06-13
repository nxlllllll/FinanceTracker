using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfer;

public sealed class TransferReadRepository(FinanceTrackerContext context) : ITransferReadRepository
{
    public async Task<TransferReadModel?> GetByIdAsync(
        Guid transferId,
        CancellationToken ct = default)
    {
        return await context.Transfers.AsNoTracking().Where(predicate: t => t.Id == transferId)
            .Select(selector: t => new TransferReadModel(
                Id: t.Id,
                UserId: t.UserId,
                FromAccountId: t.FromAccountId,
                ToAccountId: t.ToAccountId,
                AmountFrom: Money.Reconstitute(amount: t.AmountFrom, currency: t.CurrencyFrom),
                AmountTo: Money.Reconstitute(amount: t.AmountTo, currency: t.CurrencyTo),
                ExchangeRate: t.ExchangeRate,
                IsRatePending: t.IsRatePending,
                Status: t.Status,
                Description: t.Description,
                OccurredAt: t.OccurredAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

	public async Task<PagedResult<TransferReadModel>> GetAllAsync(
		Guid userId,
		Guid? accountId = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		List<string> where = ["user_id = $1"];
		List<object> args = [userId];

		if (accountId is not null)
		{
			args.Add(accountId.Value);
			where.Add($"(from_account_id = ${args.Count} OR to_account_id = ${args.Count})");
		}

		if (dateFrom is not null)
		{
			args.Add(dateFrom.Value);
			where.Add($"occurred_at >= ${args.Count}");
		}

        if (dateTo is not null)
		{
			args.Add(dateTo.Value);
			where.Add($"occurred_at <= ${args.Count}");
		}

        if (cursorOccurredAt is not null && cursorId is not null)
		{
			args.Add(cursorOccurredAt.Value);
			args.Add(cursorId.Value);
			where.Add($"(occurred_at, id) < (${args.Count - 1}, ${args.Count})");
		}

		args.Add(pageSize + 1);

		string sql = $"""
			SELECT id, user_id, from_account_id, to_account_id,
			       amount_from, currency_from, amount_to, currency_to,
			       exchange_rate, is_rate_pending, status, description, occurred_at
			FROM rm_transfers
			WHERE {String.Join(separator: " AND ", values: where)}
			ORDER BY occurred_at DESC, id DESC
			LIMIT ${args.Count}
		""";

		await using NpgsqlConnection conn = await context.OpenReadConnectionAsync(ct: ct);
		await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: sql, connection: conn);
		foreach (object arg in args)
			cmd.Parameters.AddWithValue(value: arg);

		await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken: ct);

		List<TransferReadModel> items = new List<TransferReadModel>(capacity: pageSize + 1);
		while (await reader.ReadAsync(cancellationToken: ct))
			items.Add(item: MapTransfer(reader: reader));

        bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(index: items.Count - 1);

        TransferReadModel? last = items.Count > 0 ? items[^1] : null;

        return new PagedResult<TransferReadModel>(
            Items: items.AsReadOnly(),
            HasNextPage: hasNextPage,
            NextCursorDate: hasNextPage ? last?.OccurredAt : null,
            NextCursorId: hasNextPage ? last?.Id : null
        );
    }

	private static TransferReadModel MapTransfer(NpgsqlDataReader reader)
	{
		Core.ValueObjects.Currency currencyFrom = Core.ValueObjects.Currency.Reconstitute(value: reader.GetString(ordinal: 5));
		Core.ValueObjects.Currency currencyTo = Core.ValueObjects.Currency.Reconstitute(value: reader.GetString(ordinal: 7));

		return new TransferReadModel(
			Id: reader.GetGuid(ordinal: 0),
			UserId: reader.GetGuid(ordinal: 1),
			FromAccountId: reader.GetGuid(ordinal: 2),
			ToAccountId: reader.GetGuid(ordinal: 3),
			AmountFrom: Money.Reconstitute(amount: reader.GetDecimal(ordinal: 4), currency: currencyFrom),
			AmountTo: Money.Reconstitute(amount: reader.GetDecimal(ordinal: 6), currency: currencyTo),
			ExchangeRate: reader.GetDecimal(ordinal: 8),
			IsRatePending: reader.GetBoolean(ordinal: 9),
			Status: reader.GetString(ordinal: 10).FromCode(),
			Description: reader.IsDBNull(ordinal: 11) ? null : reader.GetString(ordinal: 11),
			OccurredAt: reader.GetFieldValue<DateTimeOffset>(ordinal: 12)
		);
	}
	
    public async Task<IReadOnlyList<PendingRateTransfer>> GetPendingRateAsync(CancellationToken ct = default)
    {
        return await context.Transfers.AsNoTracking().Where(predicate: t => t.IsRatePending).Select(selector: t => new PendingRateTransfer(
            TransferId: t.Id,
            FromAccountId: t.FromAccountId,
            ToAccountId: t.ToAccountId,
            AmountFrom: t.AmountFrom,
            CurrencyFrom: t.CurrencyFrom,
            CurrencyTo: t.CurrencyTo,
            CurrentRate: t.ExchangeRate,
            RowVersion: t.RowVersion,
            OccurredAt: t.OccurredAt
        )).ToListAsync(cancellationToken: ct);
    }

    public async Task<int> GetPendingCreditCountAsync(TimeSpan gracePeriod, CancellationToken ct = default)
    {
        DateTimeOffset threshold = DateTimeOffset.UtcNow - gracePeriod;

        return await context.Transfers.AsNoTracking().CountAsync(
            predicate: t => t.Status == Core.Domains.Transfer.TransferStatus.PendingCredit && t.OccurredAt < threshold,
            cancellationToken: ct
        );
    }

    public async Task<IReadOnlyList<PendingCreditTransfer>> GetPendingCreditForCompensationAsync(
        TimeSpan compensationThreshold,
        CancellationToken ct = default)
    {
        DateTimeOffset threshold = DateTimeOffset.UtcNow - compensationThreshold;

        return await context.Transfers.AsNoTracking()
            .Where(predicate: t => t.Status == Core.Domains.Transfer.TransferStatus.PendingCredit && t.OccurredAt < threshold)
            .Select(selector: t => new PendingCreditTransfer(
                TransferId: t.Id,
                FromAccountId: t.FromAccountId,
                Amount: t.AmountFrom,
                OccurredAt: t.OccurredAt
            )).ToListAsync(cancellationToken: ct);
    }
}