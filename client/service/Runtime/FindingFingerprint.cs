using System.Security.Cryptography;
using System.Text;
using PCWachter.Contracts;

namespace AgentService.Runtime;

internal static class FindingFingerprint
{
    public static string ComputeEvidenceHash(FindingDto finding)
    {
        var builder = new StringBuilder();
        builder.Append(finding.FindingId).Append('|');
        builder.Append((int)finding.Severity).Append('|');
        builder.Append(finding.Summary).Append('|');

        foreach ((string key, string value) in finding.Evidence.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(key).Append('=').Append(value).Append(';');
        }

        byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
