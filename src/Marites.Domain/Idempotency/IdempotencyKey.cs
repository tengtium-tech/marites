namespace Marites.Domain.Idempotency;

/// <summary>
/// The recorded outcome of a command, so a retried <c>Idempotency-Key</c> returns
/// <c>AlreadyApplied</c> instead of applying the command twice (contract conventions §3.1).
/// </summary>
/// <remarks>
/// Not part of the audit, and deliberately so: <c>operation_audit</c> is append-only by
/// schema and permanent, while this record exists to be swept once the key is too old to be
/// retried. Putting it there would either break the audit's permanence or keep a retry record
/// forever.
/// </remarks>
public sealed class IdempotencyKey
{
    private IdempotencyKey()
    {
        // Materialisation only.
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>The header value as received.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>
    /// Which command this key belongs to. The same key used for a different command is a
    /// different record, because a client that reuses one key across two calls has made a
    /// mistake worth reporting rather than a retry worth honouring.
    /// </summary>
    public string Operation { get; private set; } = string.Empty;

    /// <summary>
    /// Hash of the request body. A repeat with the same key and a different hash is
    /// <c>ERR-CMN-005</c> — the same key may not mean two different things.
    /// </summary>
    public string RequestHash { get; private set; } = string.Empty;

    /// <summary>The status first returned, replayed verbatim.</summary>
    public int ResponseStatus { get; private set; }

    /// <summary>The body first returned, replayed verbatim.</summary>
    public string ResponseJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyKey Record(
        Guid tenantId,
        string key,
        string operation,
        string requestHash,
        int responseStatus,
        string responseJson,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("An idempotency record needs a key.", nameof(key));
        }

        return new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            Operation = operation,
            RequestHash = requestHash,
            ResponseStatus = responseStatus,
            ResponseJson = responseJson,
            CreatedAt = now.ToUniversalTime(),
        };
    }
}
