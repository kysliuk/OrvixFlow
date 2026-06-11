using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace OrvixFlow.Infrastructure.Services;

public class MailboxCredentialEncryptionService
{
    private readonly byte[] _key;

    public MailboxCredentialEncryptionService(IConfiguration configuration)
    {
        var keyStr = configuration["MailboxCredentialEncryptionKey"] 
                     ?? configuration["MAILBOX_CREDENTIAL_ENCRYPTION_KEY"]
                     ?? Environment.GetEnvironmentVariable("MAILBOX_CREDENTIAL_ENCRYPTION_KEY");

        if (string.IsNullOrEmpty(keyStr))
        {
            throw new InvalidOperationException("MAILBOX_CREDENTIAL_ENCRYPTION_KEY configuration or environment variable is missing.");
        }

        try
        {
            _key = Convert.FromBase64String(keyStr);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("MAILBOX_CREDENTIAL_ENCRYPTION_KEY must be a valid base64-encoded string.", ex);
        }

        if (_key.Length != 32)
        {
            throw new InvalidOperationException($"MAILBOX_CREDENTIAL_ENCRYPTION_KEY must derive a 256-bit (32 bytes) key. Current length is {_key.Length} bytes.");
        }
    }

    public string Encrypt(string plaintext)
    {
        if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[16];

        using (var aesGcm = new AesGcm(_key))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        // Combine: nonce (12) + tag (16) + ciphertext
        byte[] combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertextBase64)
    {
        if (string.IsNullOrEmpty(ciphertextBase64)) throw new ArgumentException("Ciphertext cannot be null or empty.", nameof(ciphertextBase64));

        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(ciphertextBase64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Ciphertext is not a valid base64 string.", ex);
        }

        if (combined.Length < 28) // 12 (nonce) + 16 (tag)
        {
            throw new CryptographicException("Ciphertext payload is too short or malformed.");
        }

        byte[] nonce = new byte[12];
        byte[] tag = new byte[16];
        byte[] ciphertext = new byte[combined.Length - 28];

        Buffer.BlockCopy(combined, 0, nonce, 0, 12);
        Buffer.BlockCopy(combined, 12, tag, 0, 16);
        Buffer.BlockCopy(combined, 28, ciphertext, 0, ciphertext.Length);

        byte[] plaintextBytes = new byte[ciphertext.Length];

        try
        {
            using (var aesGcm = new AesGcm(_key))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);
            }
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Decryption failed. The ciphertext may have been tampered with or the key is incorrect.", ex);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
