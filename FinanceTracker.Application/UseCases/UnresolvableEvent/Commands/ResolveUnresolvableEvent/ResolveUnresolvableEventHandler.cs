using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.UnresolvableEvent.Commands.ResolveUnresolvableEvent;

public sealed class ResolveUnresolvableEventHandler(
	IUnresolvableEventReadRepository unresolvableEventReadRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IDateProvider dateProvider
) : IRequestHandler<ResolveUnresolvableEventCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		ResolveUnresolvableEventCommand command,
		CancellationToken ct = default)
	{
		UnresolvableEventDetail? @event = await unresolvableEventReadRepository.GetByIdAsync(
			eventId: command.EventId,
			ct: ct
		);

		if (@event is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "Unresolvable event not found.", id: command.EventId));

		if (@event.ResolvedAt is not null)
			return Result<Guid, AppException>.Success(value: @event.Id);

		await unresolvableEventWriteRepository.ResolveAsync(
			id: command.EventId,
			resolvedAt: dateProvider.UtcNow,
			ct: ct
		);

		return Result<Guid, AppException>.Success(value: @event.Id);
	}
}
