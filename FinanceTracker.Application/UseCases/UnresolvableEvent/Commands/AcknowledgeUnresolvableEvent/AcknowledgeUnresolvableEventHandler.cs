using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.UnresolvableEvent.Commands.AcknowledgeUnresolvableEvent;

public sealed class AcknowledgeUnresolvableEventHandler(
	IUnresolvableEventReadRepository unresolvableEventReadRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IDateProvider dateProvider
) : IRequestHandler<AcknowledgeUnresolvableEventCommand, Result<Guid, AppException>>
{
	public async Task<Result<Guid, AppException>> Handle(
		AcknowledgeUnresolvableEventCommand command,
		CancellationToken ct = default)
	{
		UnresolvableEventDetail? @event = await unresolvableEventReadRepository.GetByIdAsync(
			eventId: command.EventId,
			ct: ct
		);

		if (@event is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "Unresolvable event not found.", id: command.EventId));

		if (@event.AcknowledgedAt is not null)
			return Result<Guid, AppException>.Success(value: @event.Id);

		await unresolvableEventWriteRepository.AcknowledgeAsync(
			id: command.EventId,
			acknowledgedAt: dateProvider.UtcNow,
			ct: ct
		);

		return Result<Guid, AppException>.Success(value: @event.Id);
	}
}
