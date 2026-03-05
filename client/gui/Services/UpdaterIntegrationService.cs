using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCWachter.Desktop.Services;

public sealed class UpdaterIntegrationService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/cOOnStar/pcwaechter-public-release/releases/latest";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool IsUpdaterInstalled => ResolveInstalledUpdaterPath() is not null;

    public async Task<UpdateCheckResult> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken)
    {
        try
        {
            string? latestVersion = await GetLatestReleaseVersionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                return UpdateCheckResult.CreateFailed("Release-Version konnte nicht ermittelt werden.");
            }

            bool isUpdateAvailable = IsNewerThanCurrent(currentVersion, latestVersion);
            return UpdateCheckResult.CreateSuccess(latestVersion, isUpdateAvailable);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.CreateFailed(ex.Message);
        }
    }

    public async Task<UpdaterExecutionResult> RunUpdateAndWaitAsync(CancellationToken cancellationToken)
    {
        string? updaterPath = ResolveInstalledUpdaterPath();
        if (string.IsNullOrWhiteSpace(updaterPath))
        {
            return UpdaterExecutionResult.CreateNotStarted("Updater wurde nicht gefunden.");
        }

        var startInfo = CreateStartInfo(updaterPath, "--update --scheduled");

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return UpdaterExecutionResult.CreateNotStarted("Updater konnte nicht gestartet werden.");
            }

            await process.WaitForExitAsync(cancellationToken);
            return UpdaterExecutionResult.CreateStarted(process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return UpdaterExecutionResult.CreateNotStarted("Updater-Lauf wurde abgebrochen.");
        }
        catch (Exception ex)
        {
            return UpdaterExecutionResult.CreateNotStarted(ex.Message);
        }
    }

    public UpdaterExecutionResult StartUpdateDetached()
    {
        string? updaterPath = ResolveInstalledUpdaterPath();
        if (string.IsNullOrWhiteSpace(updaterPath))
        {
            return UpdaterExecutionResult.CreateNotStarted("Updater wurde nicht gefunden.");
        }

        var startInfo = CreateStartInfo(updaterPath, "--update --silent");

        try
        {
            Process? process = Process.Start(startInfo);
            return process is null
                ? UpdaterExecutionResult.CreateNotStarted("Updater konnte nicht gestartet werden.")
                : UpdaterExecutionResult.CreateStarted(null);
        }
        catch (Exception ex)
        {
            return UpdaterExecutionResult.CreateNotStarted(ex.Message);
        }
    }

    private static ProcessStartInfo CreateStartInfo(string filePath, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static async Task<string?> GetLatestReleaseVersionAsync(CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        if (httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCWaechterDesktop", "1.0"));
        }

        if (httpClient.DefaultRequestHeaders.Accept.Count == 0)
        {
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        using HttpResponseMessage response = await httpClient.GetAsync(LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        GitHubReleaseDto? release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, JsonOptions, cancellationToken);
        return release?.TagName;
    }

    private static bool IsNewerThanCurrent(string currentVersion, string latestVersion)
    {
        string currentNormalized = NormalizeVersion(currentVersion);
        string latestNormalized = NormalizeVersion(latestVersion);

        if (Version.TryParse(currentNormalized, out Version? currentParsed) &&
            Version.TryParse(latestNormalized, out Version? latestParsed))
        {
            return latestParsed > currentParsed;
        }

        return !string.Equals(currentNormalized, latestNormalized, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? string.Empty
            : version.Trim().TrimStart('v', 'V');
    }

    private static string? ResolveInstalledUpdaterPath()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] candidates =
        [
            Path.Combine(programFiles, "PCWächter", "updater", "PCWaechter.Updater.exe"),
            Path.Combine(programFiles, "PCWaechter", "updater", "PCWaechter.Updater.exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
    }
}

public sealed record UpdateCheckResult(
    bool Success,
    string? LatestVersion,
    bool IsUpdateAvailable,
    string? ErrorMessage)
{
    public static UpdateCheckResult CreateSuccess(string latestVersion, bool isUpdateAvailable)
    {
        return new UpdateCheckResult(true, latestVersion, isUpdateAvailable, null);
    }

    public static UpdateCheckResult CreateFailed(string errorMessage)
    {
        return new UpdateCheckResult(false, null, false, errorMessage);
    }
}

public sealed record UpdaterExecutionResult(
    bool Started,
    int? ExitCode,
    string? ErrorMessage)
{
    public bool IsSuccessfulExit => ExitCode == 0;

    public static UpdaterExecutionResult CreateStarted(int? exitCode)
    {
        return new UpdaterExecutionResult(true, exitCode, null);
    }

    public static UpdaterExecutionResult CreateNotStarted(string errorMessage)
    {
        return new UpdaterExecutionResult(false, null, errorMessage);
    }
}
