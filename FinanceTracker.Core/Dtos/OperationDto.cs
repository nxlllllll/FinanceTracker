namespace FinanceTracker.Core.Dtos;

public sealed record OperationDto(
	Guid Id,
	OperationFilterType Type,
	string? Description,
	DateTimeOffset OccurredAt,
	TransactionDetailsDto? Transaction,
	TransferDetailsDto? Transfer
);
