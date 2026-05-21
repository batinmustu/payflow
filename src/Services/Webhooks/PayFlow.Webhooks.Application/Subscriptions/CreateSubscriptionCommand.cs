using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using PayFlow.SharedKernel;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Domain.Subscriptions;

namespace PayFlow.Webhooks.Application.Subscriptions;

public sealed record CreateSubscriptionCommand(
    Guid TenantId,
    string EventType,
    string Url) : IRequest<Result<CreateSubscriptionResponse>>;

public sealed record CreateSubscriptionResponse(
    Guid Id,
    string EventType,
    string Url,
    string Secret);

public sealed class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionValidator()
    {
        RuleFor(x => x.TenantId).NotEqual(Guid.Empty);
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Url).NotEmpty().Must(BeAbsoluteHttpUrl)
            .WithMessage("Url must be an absolute http(s) URL.");
    }

    private static bool BeAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}

internal sealed class CreateSubscriptionHandler
    : IRequestHandler<CreateSubscriptionCommand, Result<CreateSubscriptionResponse>>
{
    private readonly IWebhookSubscriptionRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateSubscriptionHandler(IWebhookSubscriptionRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result<CreateSubscriptionResponse>> Handle(
        CreateSubscriptionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 32 bytes of CSPRNG, base64'd → 43 chars. Long enough for HMAC-SHA256,
        // short enough to comfortably paste into a merchant dashboard.
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var subResult = WebhookSubscription.Create(
            tenantId: request.TenantId,
            eventType: request.EventType,
            url: request.Url,
            secret: secret);
        if (subResult.IsFailure)
            return Result.Failure<CreateSubscriptionResponse>(subResult.ErrorCode!);

        var sub = subResult.Value;
        await _repo.AddAsync(sub, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new CreateSubscriptionResponse(
            Id: sub.Id,
            EventType: sub.EventType,
            Url: sub.Url,
            Secret: sub.Secret));
        // Secret is returned EXACTLY ONCE — on creation. The list endpoint
        // omits it; merchants who lose the secret rotate the subscription.
    }
}
