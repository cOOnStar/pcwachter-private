using System.Security.Cryptography.X509Certificates;

namespace PCWaechter.LiveInstaller;

public static class Signature
{
    // Minimal heuristic (does NOT validate trust chain).
    // For real Authenticode validation you'd use WinVerifyTrust P/Invoke.
    public static string? TryGetSignerSubject(string filePath)
    {
        try
        {
            var cert = X509Certificate.CreateFromSignedFile(filePath);
            var x509 = new X509Certificate2(cert);
            return x509.Subject;
        }
        catch
        {
            return null;
        }
    }

    public static bool SubjectAllowed(string? subject, string[] allowlist)
    {
        if (allowlist.Length == 0) return true; // if not configured, don't block
        if (string.IsNullOrWhiteSpace(subject)) return false;
        return allowlist.Any(a => subject.Contains(a, StringComparison.OrdinalIgnoreCase));
    }
}
