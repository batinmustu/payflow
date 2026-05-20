namespace PayFlow.SharedKernel;

/// <summary>
/// Translates a Postgres <c>SQLSTATE 23505</c> (unique violation) into a
/// transport-neutral exception that Application-layer consumers can catch
/// without depending on EF Core / Npgsql types. Infrastructure's
/// UnitOfWork is the only thing that raises this; consumers and handlers
/// only catch it.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public string? ConstraintName { get; }

    public UniqueConstraintViolationException(string? constraintName, Exception inner)
        : base($"Unique constraint violation ({constraintName ?? "unknown"}).", inner)
    {
        ConstraintName = constraintName;
    }
}
