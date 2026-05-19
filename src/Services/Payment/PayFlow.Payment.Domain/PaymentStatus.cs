namespace PayFlow.Payment.Domain;

/// <summary>
/// The outcome of a single provider call. Maps cleanly onto the upstream
/// Transaction service's notion of attempt results (see docs/database/erd-transaction.md
/// transaction_payment_attempts.result).
/// </summary>
public enum PaymentStatus
{
    /// <summary>Authorisation succeeded, funds reserved.</summary>
    Authorized,

    /// <summary>Authorisation + capture succeeded in the same call.</summary>
    Captured,

    /// <summary>Provider refused but a different provider might accept (insufficient funds, do-not-honor).</summary>
    SoftDeclined,

    /// <summary>Provider refused and trying elsewhere will not help (invalid card, fraud).</summary>
    HardDeclined,

    /// <summary>Provider could not be reached or returned a 5xx-style failure.</summary>
    ProviderUnavailable,
}
