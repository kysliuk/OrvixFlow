namespace OrvixFlow.Api.Security;

public static class SecurityHeaderPolicies
{
    public const string ApiContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' https:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none';";
}
