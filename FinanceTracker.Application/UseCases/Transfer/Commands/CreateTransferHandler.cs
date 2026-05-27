using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Operation;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transfer.Commands;

public sealed class CreateTransferHandler(
	IAccountRepository accountRepository,
	ITransferWriteRepository transferWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	ILogger<CreateTransferHandler> logger,
	IOperationsWriteRepository operationsWriteRepository
) : IAuthorizedHandler<CreateTransferCommand, Core.Domains.Account.Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		CreateTransferCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: command.CurrencyFrom,
			toCurrency: command.CurrencyTo,
			date: DateOnly.FromDateTime(dateTime: command.OccurredAt.UtcDateTime),
			ct: ct
		);

		Result<Core.Domains.Transfer.Transfer, DomainException> transferResult = Core.Domains.Transfer.Transfer.Create(
			userId: command.UserId,
			fromAccountId: command.FromAccountId,
			toAccountId: command.ToAccountId,
			amountFrom: command.Amount,
			currencyFrom: command.CurrencyFrom,
			amountTo: command.Amount * conversion.Rate,
			currencyTo: command.CurrencyTo,
			exchangeRate: conversion.Rate,
			isRatePending: conversion.IsPending,
			description: command.Description,
			occurredAt: command.OccurredAt
		);
		if (transferResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: transferResult.Error!);
		
		Core.Domains.Transfer.Transfer transfer = transferResult.Value!;
		
		DateTimeOffset now = dateProvider.UtcNow;

		Result<Unit, DomainException> debitResult = account.DebitTransfer(
			occurredAt: now,
			transferId: transfer.Id,
			toAccountId: command.ToAccountId,
			amount: command.Amount,
			forexRate: conversion.Rate,
			description: command.Description
		);
		if (debitResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: debitResult.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transferWriteRepository.CreateAsync(transfer: transfer, ct: ct);
			await accountRepository.SaveAsync(account: account, ct: ct);
			await operationsWriteRepository.CreateFromTransferAsync(transfer: transfer, ct: ct);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to debit transfer {account.Id} > {command.ToAccountId}."),
		ct: ct);

		return Result<Guid, DomainException>.Success(value: transfer.Id);
	}
}
