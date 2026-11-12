using System.Security.Cryptography;
using System.Text;
using Marites.Infrastructure.Security;

namespace Marites.InfrastructureTests;

/// <summary>
/// The credential protector's promises: what goes in comes out, a modified envelope is
/// refused rather than decoded, and a rotation key can still open what the old key wrote.
/// </summary>
public sealed class CredentialProtectorTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void A_payload_round_trips()
    {
        var protector = new KekCredentialProtector(KekOptions.Parse(NewKey(), null));
        var payload = Encoding.UTF8.GetBytes("""{"amqp":{"user":"marites","password":"s3cret"}}""");

        var envelope = protector.Protect(payload);

        Assert.Equal(payload, protector.Unprotect(envelope));
        Assert.NotEqual(payload, envelope);
    }

    [Fact]
    public void The_same_payload_encrypts_differently_every_time()
    {
        var protector = new KekCredentialProtector(KekOptions.Parse(NewKey(), null));
        var payload = Encoding.UTF8.GetBytes("""{"key":"value"}""");

        // A fresh data key and nonce per call: equal ciphertexts would leak that two rows hold
        // the same credential, which is the whole reason ADR-018 wraps a key per row.
        Assert.NotEqual(protector.Protect(payload), protector.Protect(payload));
    }

    [Fact]
    public void A_modified_envelope_is_refused()
    {
        var protector = new KekCredentialProtector(KekOptions.Parse(NewKey(), null));
        var envelope = protector.Protect(Encoding.UTF8.GetBytes("""{"key":"value"}"""));

        envelope[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(envelope));
    }

    [Fact]
    public void A_truncated_envelope_is_refused()
    {
        var protector = new KekCredentialProtector(KekOptions.Parse(NewKey(), null));

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(new byte[8]));
    }

    [Fact]
    public void Another_key_cannot_open_it()
    {
        var envelope = new KekCredentialProtector(KekOptions.Parse(NewKey(), null))
            .Protect(Encoding.UTF8.GetBytes("""{"key":"value"}"""));

        var stranger = new KekCredentialProtector(KekOptions.Parse(NewKey(), null));

        Assert.ThrowsAny<CryptographicException>(() => stranger.Unprotect(envelope));
    }

    [Fact]
    public void The_previous_key_still_opens_what_it_wrote()
    {
        var oldKey = NewKey();
        var written = new KekCredentialProtector(KekOptions.Parse(oldKey, null))
            .Protect(Encoding.UTF8.GetBytes("""{"key":"value"}"""));

        // Rotation: the new key is current, the old one is still accepted on unwrap.
        var rotating = new KekCredentialProtector(KekOptions.Parse(NewKey(), oldKey));

        Assert.Equal(Encoding.UTF8.GetBytes("""{"key":"value"}"""), rotating.Unprotect(written));
    }

    [Fact]
    public void A_missing_key_is_refused_with_the_reason()
    {
        var error = Assert.Throws<InvalidOperationException>(() => KekOptions.Parse(null, null));

        Assert.Contains("MARITES_KEK", error.Message);
        Assert.Contains("ADR-018", error.Message);
    }

    [Fact]
    public void A_key_of_the_wrong_length_is_refused()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        Assert.Throws<InvalidOperationException>(() => KekOptions.Parse(shortKey, null));
    }

    [Fact]
    public void A_kms_uri_is_refused_rather_than_parsed_as_base64()
    {
        var error = Assert.Throws<NotSupportedException>(
            () => KekOptions.Parse("kms://projects/marites/keys/kek", null));

        Assert.Contains("not implemented in M0", error.Message);
    }
}
