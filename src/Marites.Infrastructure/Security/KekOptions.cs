using System.Security.Cryptography;

namespace Marites.Infrastructure.Security;

/// <summary>
/// The key-encryption key, read from <c>MARITES_KEK</c> and optionally
/// <c>MARITES_KEK_PREVIOUS</c> (configuration reference §1).
/// </summary>
/// <remarks>
/// Base64 only in M0. The reference also permits a KMS URI; that path needs a client and a
/// credential of its own, so a URI is refused with a message that says so rather than being
/// parsed as base64 and failing somewhere less obvious.
/// </remarks>
public sealed class KekOptions
{
    public const int KeySizeBytes = 32;

    private KekOptions(byte[] current, byte[]? previous)
    {
        Current = current;
        Previous = previous;
    }

    public byte[] Current { get; }

    /// <summary>Present only during rotation. Tried when the current key does not unwrap a value.</summary>
    public byte[]? Previous { get; }

    public static KekOptions FromEnvironment(Func<string, string?> read) =>
        Parse(read("MARITES_KEK"), read("MARITES_KEK_PREVIOUS"));

    public static KekOptions Parse(string? current, string? previous)
    {
        var key = ParseKey(current, "MARITES_KEK", required: true)!;
        var old = ParseKey(previous, "MARITES_KEK_PREVIOUS", required: false);

        return new KekOptions(key, old);
    }

    private static byte[]? ParseKey(string? value, string name, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (!required)
            {
                return null;
            }

            throw new InvalidOperationException(
                $"{name} is required from first start: it wraps the per-row data keys that encrypt broker credentials (ADR-018). " +
                "Generate one with 32 random bytes, base64-encoded.");
        }

        if (value.Contains("://", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"{name} is a URI. KMS references are permitted by the configuration reference but not implemented in M0; " +
                "supply a base64 32-byte key instead.");
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{name} is not valid base64.");
        }

        if (decoded.Length != KeySizeBytes)
        {
            throw new InvalidOperationException(
                $"{name} decodes to {decoded.Length} bytes; AES-256 needs exactly {KeySizeBytes}.");
        }

        return decoded;
    }
}
