namespace FinanceTracker.Core.Dtos;

public sealed record OperationDto(
	Guid Id,
	OperationFilterType Type,
	string? Description,
	DateTime OccurredAt,
	TransactionDetailsDto? Transaction,
	TransferDetailsDto? Transfer
);