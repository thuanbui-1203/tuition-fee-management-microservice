using System.Security.Cryptography;

namespace Microservices.Common.Security;

/// <summary>
/// Authenticated encryption helper (AES-256-GCM). Used only for fields that must be
/// recoverable by another trusted service (for example, the plaintext OTP inside an
/// outbox event consumed by the notification service). OTPs and passwords that only need
/// verification are hashed, never encrypted.
///
/// Ciphertext layout: [nonce (12 bytes)][tag (16 bytes)][ciphertext].
/// </summary>
public static class AesGcmCrypto
{
    public const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    public static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ValidateKey(key);

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSizeBytes + TagSizeBytes, ciphertext.Length);
        return result;
    }

    public static byte[] Decrypt(byte[] payload, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateKey(key);

        if (payload.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Ciphertext is too short to be valid.");
        }

        var nonce = payload.AsSpan(0, NonceSizeBytes).ToArray();
        var tag = payload.AsSpan(NonceSizeBytes, TagSizeBytes).ToArray();
        var ciphertext = payload.AsSpan(NonceSizeBytes + TagSizeBytes).ToArray();
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    public static string EncryptToString(string plaintext, byte[] key) =>
        Convert.ToBase64String(Encrypt(System.Text.Encoding.UTF8.GetBytes(plaintext), key));

    public static string DecryptString(string payload, byte[] key) =>
        System.Text.Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(payload), key));

    /// <summary>Generates a new random 256-bit key, suitable for configuration.</summary>
    public static string GenerateKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeySizeBytes));

    private static void ValidateKey(byte[] key)
    {
        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException($"AES-GCM requires a {KeySizeBytes}-byte key.", nameof(key));
        }
    }
}
