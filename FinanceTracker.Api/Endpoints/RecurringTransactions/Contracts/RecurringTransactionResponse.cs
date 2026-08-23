using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;

/// <summary>
/// HTTP projection of <see cref="RecurringTransactionReadModel"/>
/// </summary>
public sealed record RecurringTransactionResponse(
	Guid Id,
	Guid AccountId,
	Guid CategoryId,
	Money Amount,
	DirectionType Direction,
	int DayOfMonth,
	DateTimeOffset NextDueAtUtc,
	DateOnly NextDueLocalDate,
	string? Description,
	bool IsActive,
	DateTimeOffset? LastExecutedAt,
	DateTimeOffset? LastMissedAt,
	DateTimeOffset CreatedAt
) : IResponseOf<RecurringTransactionReadModel, RecurringTransactionResponse>
{
	public static RecurringTransactionResponse FromReadModel(RecurringTransactionReadModel readModel) => new RecurringTransactionResponse(
		Id: readModel.Id,
		AccountId: readModel.AccountId,
		CategoryId: readModel.CategoryId,
		Amount: readModel.Amount,
		Direction: readModel.Direction,
		DayOfMonth: readModel.DayOfMonth,
		NextDueAtUtc: readModel.NextDueAtUtc,
		NextDueLocalDate: DateOnly.FromDateTime(dateTime: TimeZoneInfo.ConvertTime(
			dateTimeOffset: readModel.NextDueAtUtc,
			destinationTimeZone: readModel.TimeZone.ToTimeZoneInfo()
		).Date),
		Description: readModel.Description,
		IsActive: readModel.IsActive,
		LastExecutedAt: readModel.LastExecutedAt,
		LastMissedAt: readModel.LastMissedAt,
		CreatedAt: readModel.CreatedAt
	);
}
