using System.Security.Cryptography;
using Marites.Application.Security;

namespace Marites.Infrastructure.Security;

/// <summary>
/// AES-256-GCM with a per-row data key wrapped by the key-encryption key (ADR-018).
/// </summary>
/// <remarks>
/// <para>
/// Envelope layout — every encrypted value is self-contained, so a row can be read without
/// consulting anything but the KEK:
/// </para>
/// <code>
/// version(1) | kekNonce(12) | wrappedKey(32) | keyTag(16) | payloadNonce(12) | payloadTag(16) | ciphertext(n)
/// </code>
/// <para>
/// A fresh data key per value is the point: recovering one credential does not yield the rest,
/// and rotating the KEK re-wraps 32 bytes per row rather than re-encrypting the payload.
/// GCM rather than CBC because a modified ciphertext must fail loudly — a credential that
/// silently decrypts to garbage is worse than one that refuses.
/// </para>
/// </remarks>
public sealed class KekCredentialProtector : ICredentialProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int DataKeySize = 32;

    private const int HeaderSize = 1 + NonceSize + DataKeySize + TagSize + NonceSize + TagSize;

    private readonly KekOptions _keys;

    public KekCredentialProtector(KekOptions keys)
    {
        _keys = keys;
    }

    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        var dataKey = RandomNumberGenerator.GetBytes(DataKeySize);
        var kekNonce = RandomNumberGenerator.GetBytes(NonceSize);
        var payloadNonce = RandomNumberGenerator.GetBytes(NonceSize);

        var wrappedKey = new byte[DataKeySize];
        var keyTag = new byte[TagSize];
        using (var kek = new AesGcm(_keys.Current, TagSize))
        {
            kek.Encrypt(kekNonce, dataKey, wrappedKey, keyTag);
        }

        var ciphertext = new byte[plaintext.Length];
        var payloadTag = new byte[TagSize];
        using (var payload = new AesGcm(dataKey, TagSize))
        {
            payload.Encrypt(payloadNonce, plaintext, ciphertext, payloadTag);
        }

        CryptographicOperations.ZeroMemory(dataKey);

        var envelope = new byte[HeaderSize + ciphertext.Length];
        var span = envelope.AsSpan();
        span[0] = Version;
        kekNonce.CopyTo(span[1..]);
        wrappedKey.CopyTo(span[(1 + NonceSize)..]);
        keyTag.CopyTo(span[(1 + NonceSize + DataKeySize)..]);
        payloadNonce.CopyTo(span[(1 + NonceSize + DataKeySize + TagSize)..]);
        payloadTag.CopyTo(span[(1 + NonceSize + DataKeySize + TagSize + NonceSize)..]);
        ciphertext.CopyTo(span[HeaderSize..]);

        return envelope;
    }

    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length < HeaderSize)
        {
            throw new CryptographicException("The credential envelope is truncated.");
        }

        if (ciphertext[0] != Version)
        {
            throw new CryptographicException($"Unknown credential envelope version {ciphertext[0]}.");
        }

        var kekNonce = ciphertext.Slice(1, NonceSize);
        var wrappedKey = ciphertext.Slice(1 + NonceSize, DataKeySize);
        var keyTag = ciphertext.Slice(1 + NonceSize + DataKeySize, TagSize);
        var payloadNonce = ciphertext.Slice(1 + NonceSize + DataKeySize + TagSize, NonceSize);
        var payloadTag = ciphertext.Slice(1 + NonceSize + DataKeySize + TagSize + NonceSize, TagSize);
        var body = ciphertext[HeaderSize..];

        var dataKey = Unwrap(kekNonce, wrappedKey, keyTag);

        try
        {
            var plaintext = new byte[body.Length];
            using var payload = new AesGcm(dataKey, TagSize);
            payload.Decrypt(payloadNonce, body, payloadTag, plaintext);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>
    /// Tries the current key, then the previous one. The previous key exists only during
    /// rotation (runbook §8): values it still wraps are re-wrapped on their next write, which is
    /// what lets <c>rotate-kek</c> report zero rows remaining.
    /// </summary>
    private byte[] Unwrap(ReadOnlySpan<byte> kekNonce, ReadOnlySpan<byte> wrappedKey, ReadOnlySpan<byte> keyTag)
    {
        var dataKey = new byte[DataKeySize];

        using (var kek = new AesGcm(_keys.Current, TagSize))
        {
            try
            {
                kek.Decrypt(kekNonce, wrappedKey, keyTag, dataKey);
                return dataKey;
            }
            catch (CryptographicException)
            {
                // Fall through to the previous key, if there is one.
            }
        }

        if (_keys.Previous is { } previous)
        {
            using var kek = new AesGcm(previous, TagSize);
            kek.Decrypt(kekNonce, wrappedKey, keyTag, dataKey);
            return dataKey;
        }

        throw new CryptographicException(
            "The credential could not be decrypted with the current key. If the KEK was rotated, set MARITES_KEK_PREVIOUS.");
    }
}
