using FinanceTracker.Core.ReadModels;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

internal sealed class HistoryQuery
{
    private const string TransactionColumns = """
        id AS Id,
        'Transaction' AS Type,
        description AS Description,
        occurred_at AS OccurredAt,
        account_id AS AccountId,
        category_id AS CategoryId,
        amount AS Amount,
        currency_code AS CurrencyCode,
        direction_type AS Direction,
        is_excluded AS IsExcluded,
        NULL::uuid AS FromAccountId,
        NULL::uuid AS ToAccountId,
        NULL::numeric AS AmountFrom,
        NULL::varchar AS CurrencyFrom,
        NULL::numeric AS AmountTo,
        NULL::varchar AS CurrencyTo
    """;

    private const string TransferColumns = """
        id AS Id,
        'Transfer' AS Type,
        description AS Description,
        occurred_at AS OccurredAt,
        NULL::uuid AS AccountId,
        NULL::uuid AS CategoryId,
        NULL::numeric AS Amount,
        NULL::varchar AS CurrencyCode,
        NULL::varchar AS Direction,
        NULL::boolean AS IsExcluded,
        from_account_id AS FromAccountId,
        to_account_id AS ToAccountId,
        amount_from AS AmountFrom,
        currency_from::varchar AS CurrencyFrom,
        amount_to AS AmountTo,
        currency_to::varchar AS CurrencyTo
    """;

    public string Sql { get; }
    public IReadOnlyList<NpgsqlParameter> Parameters { get; }

    private HistoryQuery(string sql, List<NpgsqlParameter> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    public static HistoryQuery Build(
        Guid userId,
        OperationFilterType? type,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        DateTimeOffset? cursorOccurredAt,
        Guid? cursorId,
        int limit)
    {
        List<NpgsqlParameter> parameters =
        [
            new NpgsqlParameter(parameterName: "@userId", value: userId),
            new NpgsqlParameter(parameterName: "@limit",  value: limit)
        ];

        string sharedFilters = BuildSharedFilters(
            parameters: parameters,
            dateFrom: dateFrom,
            dateTo: dateTo,
            cursorOccurredAt: cursorOccurredAt,
            cursorId: cursorId
        );

        string sql = type switch
        {
            OperationFilterType.Income => BuildTransactionsOnly(direction: "credit", sharedFilters: sharedFilters),
            OperationFilterType.Expense => BuildTransactionsOnly(direction: "debit",  sharedFilters: sharedFilters),
            OperationFilterType.Transfer => BuildTransfersOnly(sharedFilters: sharedFilters),
            _ => BuildUnionAll(sharedFilters: sharedFilters)
        };

        return new HistoryQuery(sql: sql, parameters: parameters);
    }

    private static string BuildSharedFilters(
        List<NpgsqlParameter> parameters,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        DateTimeOffset? cursorOccurredAt,
        Guid? cursorId)
    {
        List<string> clauses = [];

        if (dateFrom is not null)
        {
            clauses.Add("AND occurred_at >= @dateFrom");
            parameters.Add(new NpgsqlParameter("@dateFrom", dateFrom.Value));
        }

        if (dateTo is not null)
        {
            clauses.Add("AND occurred_at <= @dateTo");
            parameters.Add(new NpgsqlParameter("@dateTo", dateTo.Value));
        }

        if (cursorOccurredAt is not null && cursorId is not null)
        {
            clauses.Add("AND (occurred_at < @cursorDate OR (occurred_at = @cursorDate AND id < @cursorId))");
            parameters.Add(new NpgsqlParameter("@cursorDate", cursorOccurredAt.Value));
            parameters.Add(new NpgsqlParameter("@cursorId",   cursorId.Value));
        }

        return string.Join("\n  ", clauses);
    }

    private static string BuildTransactionsOnly(string direction, string sharedFilters) => $"""
        SELECT {TransactionColumns}
        FROM rm_transactions
        WHERE user_id = @userId
          AND direction_type = '{direction}'
          {sharedFilters}
        ORDER BY OccurredAt DESC, id DESC
        LIMIT @limit
    """;

    private static string BuildTransfersOnly(string sharedFilters) => $"""
        SELECT {TransferColumns}
        FROM rm_transfers
        WHERE user_id = @userId
          {sharedFilters}
        ORDER BY OccurredAt DESC, id DESC
        LIMIT @limit
    """;

    private static string BuildUnionAll(string sharedFilters) => $"""
        SELECT {TransactionColumns}
        FROM rm_transactions
        WHERE user_id = @userId
          {sharedFilters}

        UNION ALL

        SELECT {TransferColumns}
        FROM rm_transfers
        WHERE user_id = @userId
          {sharedFilters}

        ORDER BY OccurredAt DESC, Id DESC
        LIMIT @limit
    """;
}