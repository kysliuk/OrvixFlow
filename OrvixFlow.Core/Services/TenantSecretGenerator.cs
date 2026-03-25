using System;
using System.Security.Cryptography;

namespace OrvixFlow.Core.Services;

public static class TenantSecretGenerator
{
    public static string GenerateWebhookSecret(int keySizeBytes = 32)
    {
        var key = RandomNumberGenerator.GetBytes(keySizeBytes);
        return Convert.ToBase64String(key);
    }
}
