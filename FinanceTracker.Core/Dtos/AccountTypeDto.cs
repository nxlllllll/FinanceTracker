namespace FinanceTracker.Core.Dtos;

public sealed record AccountTypeDto(
	string Type,
	string Name,
	string? Description
);
