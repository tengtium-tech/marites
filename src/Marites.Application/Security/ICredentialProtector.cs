namespace Marites.Application.Security;

/// <summary>
/// Encrypts and decrypts the provider-shaped credential payload stored in
/// <c>broker_profile.credentials_enc</c> (ADR-018, NFR-003).
/// </summary>
/// <remarks>
/// The port is deliberately byte-oriented rather than JSON-shaped: what a credential payload
/// contains is the adapter's business, and a port that knew about it would be a second place
/// the credential vocabulary is defined.
/// </remarks>
public interface ICredentialProtector
{
    /// <summary>Encrypts a payload. The result is self-contained: it carries everything needed to decrypt it.</summary>
    byte[] Protect(ReadOnlySpan<byte> plaintext);

    /// <summary>
    /// Decrypts a payload. Throws <see cref="System.Security.Cryptography.CryptographicException"/>
    /// when the value was not produced by this key material or has been altered.
    /// </summary>
    byte[] Unprotect(ReadOnlySpan<byte> ciphertext);
}
