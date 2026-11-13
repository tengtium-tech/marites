using System.Security.Cryptography;
using System.Text;

namespace Marites.Application.Queues;

/// <summary>
/// The public identifier of a queue, derived from the entity's identity (DEC-022).
/// </summary>
/// <remarks>
/// A queue has no stored row until capture is configured in M2, so a random key would change the
/// public id the moment someone enabled capture. Deriving it means the id a client saw before is
/// the id it sees after — and the id <c>capture_rule</c> will reference.
///
/// UUIDv5 (RFC 4122): SHA-1 over the namespace bytes followed by the name, with the version and
/// variant bits overwritten. Deterministic across processes and languages, which a hash of the
/// concatenated string alone would not guarantee.
/// </remarks>
public static class QueueIdentity
{
    /// <summary>Published in DEC-022 so every implementation computes the same ids.</summary>
    public static readonly Guid Namespace = new("6f1c4a52-9d3b-5e77-9f0a-2b8c7d4e1a35");

    public static Guid Compute(Guid brokerProfileId, string? vhost, string name, string sourceKind)
    {
        // Canonical forms matter: two spellings of one identity would produce two ids.
        var canonical = string.Join(
            '|',
            brokerProfileId.ToString("D"),
            vhost ?? string.Empty,
            name,
            sourceKind.ToLowerInvariant());

        var namespaceBytes = Namespace.ToByteArray(bigEndian: true);
        var nameBytes = Encoding.UTF8.GetBytes(canonical);

        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);

        var hash = SHA1.HashData(input);

        var result = hash[..16];
        result[6] = (byte)((result[6] & 0x0F) | 0x50); // version 5
        result[8] = (byte)((result[8] & 0x3F) | 0x80); // RFC 4122 variant

        return new Guid(result, bigEndian: true);
    }
}
