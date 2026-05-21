using MediatR;
using PayFlow.SharedKernel;
using PayFlow.Webhooks.Application.Abstractions;

namespace PayFlow.Webhooks.Application.Subscriptions;

public sealed record DeactivateSubscriptionCommand(Guid TenantId, Guid SubscriptionId)
    : IRequest<Result>;

internal sealed class DeactivateSubscriptionHandler
    : IRequestHandler<DeactivateSubscriptionCommand, Result>
{
    private readonly IWebhookSubscriptionRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeactivateSubscriptionHandler(IWebhookSubscriptionRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result> Handle(DeactivateSubscriptionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sub = await _repo.GetAsync(request.TenantId, request.SubscriptionId, ct);
        if (sub is null) return Result.Failure("NOT_FOUND");

        sub.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
