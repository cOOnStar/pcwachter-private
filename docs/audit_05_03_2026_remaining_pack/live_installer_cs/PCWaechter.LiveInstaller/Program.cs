using System.Diagnostics;
using System.Text.Json;

namespace PCWaechter.LiveInstaller;

internal static class Program
{
    // Variante A: stable latest/download (no API calls)
    private const string DefaultReleaseBase = "https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download";
    private const string ManifestName = "installer-manifest.json";

    public static async Task<int> Main(string[] args)
    {
        var releaseBase = Environment.GetEnvironmentVariable("PCW_RELEASE_BASE_URL") ?? DefaultReleaseBase;
        var manifestUrl = $"{releaseBase}/{ManifestName}";

        Console.WriteLine($"Manifest: {manifestUrl}");

        using var http = new HttpClient();

        InstallerManifest manifest;
        try
        {
            var json = await http.GetStringAsync(manifestUrl);
            manifest = JsonSerializer.Deserialize<InstallerManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception("Manifest deserialize failed");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load manifest: {ex.Message}");
            return 10;
        }

        if (string.IsNullOrWhiteSpace(manifest.Offline.Url) || string.IsNullOrWhiteSpace(manifest.Offline.Sha256))
        {
            Console.Error.WriteLine("Manifest missing offline.url or offline.sha256");
            return 11;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "PCWaechter", manifest.Version);
        var installerPath = Path.Combine(tempDir, manifest.Offline.Name);

        Console.WriteLine($"Downloading offline installer to: {installerPath}");

        try
        {
            var prog = new Progress<long>(b => {
                if (manifest.Offline.SizeBytes > 0)
                    Console.Write($"\r{b}/{manifest.Offline.SizeBytes} bytes");
                else
                    Console.Write($"\r{b} bytes");
            });

            await Download.DownloadFileAsync(http, manifest.Offline.Url, installerPath, prog);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Download failed: {ex.Message}");
            return 12;
        }

        // Hash verify
        Console.WriteLine("Verifying SHA256...");
        var got = Hashing.Sha256Hex(installerPath);
        if (!Hashing.EqualsHex(got, manifest.Offline.Sha256))
        {
            Console.Error.WriteLine($"SHA256 mismatch! expected={manifest.Offline.Sha256} got={got}");
            return 13;
        }

        // Optional signature heuristic
        if (manifest.Offline.SignatureRequired)
        {
            Console.WriteLine("Checking signature subject...");
            var subject = Signature.TryGetSignerSubject(installerPath);
            if (!Signature.SubjectAllowed(subject, manifest.Offline.SignatureSubjectAllowlist))
            {
                Console.Error.WriteLine($"Signature subject not allowed: {subject ?? "(none)"}");
                return 14;
            }
        }

        Console.WriteLine("Starting installer...");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true, // triggers UAC if needed
                Arguments = "" // e.g. "/S" for silent if your installer supports it
            };
            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start installer: {ex.Message}");
            return 15;
        }
    }
}
