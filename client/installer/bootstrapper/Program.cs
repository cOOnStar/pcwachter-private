using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Win32;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

const string DefaultManifestUrl = "https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download/installer-manifest.json";
const string DefaultInstallerFileName = "PCWaechter_offline_installer_0000.exe";
const string DefaultRuntimeMinVersion = "10.0";
const string DefaultRuntimeInstallerUrl = "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe";
const string DefaultRuntimeInstallerArgs = "/install /quiet /norestart";
var versionedOfflineInstallerRegex = new Regex(
    "^PCWaechter_offline_installer(_[0-9]{4}|_[0-9]+\\.[0-9]+\\.[0-9]+)?\\.exe$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

var argsSet = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);
string? manifestUrlArg = GetArgumentValue(args, "--manifest-url");
bool skipSelfUpdate = argsSet.Contains("--skip-self-update");
bool useLocalInstaller = argsSet.Contains("--use-local-installer");

string manifestUrl = string.IsNullOrWhiteSpace(manifestUrlArg) ? DefaultManifestUrl : manifestUrlArg;
Console.WriteLine($"[Bootstrapper] Primäres Manifest: {manifestUrl}");

try
{
    var bootstrapperVersion = GetCurrentVersion();
    Console.WriteLine($"[Bootstrapper] Version: {bootstrapperVersion}");

    if (useLocalInstaller && TryFindLocalInstaller(versionedOfflineInstallerRegex, out var localInstallerPath))
    {
        Console.WriteLine($"[Bootstrapper] Lokales Setup gefunden: {localInstallerPath}");
        AppendBootstrapperLog($"Lokales Setup verwendet: {localInstallerPath}");
        RunInstallerAndWait(localInstallerPath, string.Empty, deleteAfterSuccess: false);
        Console.WriteLine("[Bootstrapper] Lokale Installation abgeschlossen.");
        return;
    }

    using var http = CreateHttpClient();
    var manifest = await FetchManifestWithFallbackAsync(http, manifestUrl, manifestUrlArg);

    if (!skipSelfUpdate)
    {
        bool restarted = await TrySelfUpdateAsync(http, manifest, bootstrapperVersion, manifestUrl);
        if (restarted)
        {
            Console.WriteLine("[Bootstrapper] Neustart in neuer Bootstrapper-Version ausgelöst.");
            return;
        }
    }

    await EnsureWindowsDesktopRuntimeAsync(http, manifest);

    var installerPackage = await ResolveInstallerPackageAsync(http, manifest);
    string installerTarget = Path.Combine(Path.GetTempPath(), installerPackage.FileName);
    Console.WriteLine($"[Bootstrapper] Lade Installer nach: {installerTarget}");
    using (IDownloadProgressSink progressSink = CreateProgressSink())
    {
        await DownloadFileAsync(http, installerPackage.Url, installerTarget, progressSink.Report);
        progressSink.Complete();
    }

    if (!string.IsNullOrWhiteSpace(installerPackage.Sha256))
    {
        ValidateSha256(installerTarget, installerPackage.Sha256);
        Console.WriteLine("[Bootstrapper] SHA-256 Validierung erfolgreich.");
    }

    string installArgs = string.IsNullOrWhiteSpace(installerPackage.SilentArgs)
        ? string.Empty
        : installerPackage.SilentArgs;

    Console.WriteLine($"[Bootstrapper] Starte Installer: {installerPackage.FileName} {installArgs}");
    RunInstallerAndWait(installerTarget, installArgs, deleteAfterSuccess: true);
    Console.WriteLine("[Bootstrapper] Installation abgeschlossen.");
}
catch (Exception ex)
{
    HandleFatalError(ex);
    Environment.ExitCode = 1;
}

static void HandleFatalError(Exception ex)
{
    string message = "[Bootstrapper] Fehler: " + ex.Message;
    Console.Error.WriteLine(message);

    try
    {
        string logPath = Path.Combine(Path.GetTempPath(), "pcwaechter-bootstrapper.log");
        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");

        if (Environment.UserInteractive)
        {
            NativeMessageBox.Show($"{ex.Message}\n\nDetails: {logPath}", "PC Wächter Installer", 0x00000010);
        }
    }
    catch
    {
    }
}

static void AppendBootstrapperLog(string message)
{
    try
    {
        string logPath = Path.Combine(Path.GetTempPath(), "pcwaechter-bootstrapper.log");
        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }
    catch
    {
    }
}

static Version GetCurrentVersion()
{
    var assembly = typeof(Program).Assembly;
    return assembly.GetName().Version ?? new Version(0, 0, 0, 0);
}

static HttpClient CreateHttpClient()
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCWaechter-InstallerBootstrapper", "1.0"));
    return client;
}

static async Task<InstallerManifest> FetchManifestWithFallbackAsync(HttpClient http, string primaryManifestUrl, string? manifestUrlArg)
{
    var candidates = new List<string> { primaryManifestUrl };

    Exception? lastError = null;
    foreach (var manifestUrl in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        try
        {
            Console.WriteLine($"[Bootstrapper] Versuche Manifest-URL: {manifestUrl}");
            string json = await http.GetStringAsync(manifestUrl);
            var manifest = JsonSerializer.Deserialize(json, BootstrapperJsonContext.Default.InstallerManifest);

            if (manifest == null)
            {
                throw new InvalidOperationException("Manifest konnte nicht gelesen werden.");
            }

            return manifest;
        }
        catch (Exception ex)
        {
            lastError = ex;
            Console.WriteLine($"[Bootstrapper] Manifest-URL fehlgeschlagen: {manifestUrl}");
        }
    }

    throw new InvalidOperationException(
        "Manifest konnte nicht geladen werden. Bei privaten GitHub-Repositories liefern öffentliche URLs 404. " +
        "Nutze ein öffentliches Hosting für installer-manifest.json oder starte mit --manifest-url <URL>.",
        lastError);
}

static bool TryFindLocalInstaller(Regex versionedOfflineInstallerRegex, out string installerPath)
{
    string baseDir = AppContext.BaseDirectory;
    var versionedCandidate = Directory
        .EnumerateFiles(baseDir, "PCWaechter_offline_installer*.exe", SearchOption.TopDirectoryOnly)
        .FirstOrDefault(path => versionedOfflineInstallerRegex.IsMatch(Path.GetFileName(path)));

    if (!string.IsNullOrWhiteSpace(versionedCandidate) && File.Exists(versionedCandidate))
    {
        installerPath = versionedCandidate;
        return true;
    }

    installerPath = string.Empty;
    return false;
}

static void RunInstallerAndWait(string installerPath, string installArgs, bool deleteAfterSuccess)
{
    var installProcess = Process.Start(new ProcessStartInfo
    {
        FileName = installerPath,
        Arguments = installArgs,
        UseShellExecute = true
    });

    if (installProcess == null)
    {
        throw new InvalidOperationException("Installer konnte nicht gestartet werden.");
    }

    Console.WriteLine("[Bootstrapper] Installer wurde gestartet. Warte auf Abschluss...");
    installProcess.WaitForExit();

    if (installProcess.ExitCode != 0)
    {
        throw new InvalidOperationException($"Installer fehlgeschlagen (ExitCode {installProcess.ExitCode}).");
    }

    if (deleteAfterSuccess)
    {
        TryDeleteFileWithRetries(installerPath);
    }
}

static async Task<bool> TrySelfUpdateAsync(HttpClient http, InstallerManifest manifest, Version currentVersion, string manifestUrl)
{
    var self = manifest.Bootstrapper;
    if (self == null || string.IsNullOrWhiteSpace(self.Version) || string.IsNullOrWhiteSpace(self.Url))
    {
        return false;
    }

    if (!TryParseVersion(self.Version, out var remoteVersion))
    {
        Console.WriteLine("[Bootstrapper] Bootstrapper-Version aus Manifest ist ungültig, überspringe Self-Update.");
        return false;
    }

    if (remoteVersion <= currentVersion)
    {
        Console.WriteLine("[Bootstrapper] Keine neuere Bootstrapper-Version verfügbar.");
        return false;
    }

    string tempExe = Path.Combine(Path.GetTempPath(), $"PCWaechter_live_installer_{remoteVersion}.exe");
    Console.WriteLine($"[Bootstrapper] Neue Bootstrapper-Version gefunden ({remoteVersion}), lade herunter...");
    await DownloadFileAsync(http, self.Url, tempExe);

    if (!string.IsNullOrWhiteSpace(self.Sha256))
    {
        ValidateSha256(tempExe, self.Sha256);
    }

    string forwardedManifestUrlArg = string.IsNullOrWhiteSpace(manifestUrl)
        ? string.Empty
        : $" --manifest-url \"{manifestUrl}\"";

    Process.Start(new ProcessStartInfo
    {
        FileName = tempExe,
        Arguments = "--skip-self-update" + forwardedManifestUrlArg,
        UseShellExecute = true
    });

    return true;
}

static async Task<InstallerPackage> ResolveInstallerPackageAsync(HttpClient http, InstallerManifest manifest)
{
    if (manifest.Installer == null || string.IsNullOrWhiteSpace(manifest.Installer.Url))
    {
        throw new InvalidOperationException("Manifest muss installer.url enthalten.");
    }

    string installerFileName = GetFileNameFromUrl(manifest.Installer.Url, DefaultInstallerFileName);

    return new InstallerPackage(
        manifest.Installer.Url,
        installerFileName,
        manifest.Installer?.Sha256,
        manifest.Installer?.SilentArgs);
}

static async Task DownloadFileAsync(HttpClient http, string url, string targetPath, Action<DownloadProgress>? onProgress = null)
{
    using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    long? totalBytes = response.Content.Headers.ContentLength;

    await using var input = await response.Content.ReadAsStreamAsync();
    await using var output = File.Create(targetPath);

    byte[] buffer = new byte[81920];
    long downloadedBytes = 0;
    while (true)
    {
        int bytesRead = await input.ReadAsync(buffer.AsMemory(0, buffer.Length));
        if (bytesRead <= 0)
        {
            break;
        }

        await output.WriteAsync(buffer.AsMemory(0, bytesRead));
        downloadedBytes += bytesRead;
        onProgress?.Invoke(new DownloadProgress(downloadedBytes, totalBytes));
    }
}

static IDownloadProgressSink CreateProgressSink(string title = "Die neueste Version des PC Wächters wird heruntergeladen, gleich geht's los...")
{
    if (!Environment.UserInteractive)
    {
        AppendBootstrapperLog("Progress-Sink: Konsole (nicht interaktiv)");
        return new ConsoleDownloadProgressRenderer(title);
    }

    try
    {
        AppendBootstrapperLog("Progress-Sink: Shell-Dialog");
        return new ShellDownloadProgressRenderer(title);
    }
    catch (Exception ex)
    {
        AppendBootstrapperLog("Progress-Sink Shell-Dialog fehlgeschlagen: " + ex);
    }

    try
    {
        AppendBootstrapperLog("Progress-Sink: Externes Fortschrittsfenster");
        return new ExternalProgressWindowRenderer(title);
    }
    catch (Exception ex)
    {
        AppendBootstrapperLog("Progress-Sink Fallback auf Konsole: " + ex);
        return new ConsoleDownloadProgressRenderer(title);
    }
}

static async Task EnsureWindowsDesktopRuntimeAsync(HttpClient http, InstallerManifest manifest)
{
    bool enabled = manifest.Runtime?.Enabled ?? true;
    if (!enabled)
    {
        Console.WriteLine("[Bootstrapper] Runtime-Installationsprüfung deaktiviert.");
        return;
    }

    string minimumVersion = string.IsNullOrWhiteSpace(manifest.Runtime?.MinVersion)
        ? DefaultRuntimeMinVersion
        : manifest.Runtime!.MinVersion!;

    if (IsWindowsDesktopRuntimeInstalled(minimumVersion))
    {
        Console.WriteLine($"[Bootstrapper] WindowsDesktop-Runtime >= {minimumVersion} bereits vorhanden.");
        return;
    }

    string runtimeUrl = string.IsNullOrWhiteSpace(manifest.Runtime?.Url)
        ? DefaultRuntimeInstallerUrl
        : manifest.Runtime!.Url!;
    string runtimeArgs = string.IsNullOrWhiteSpace(manifest.Runtime?.SilentArgs)
        ? DefaultRuntimeInstallerArgs
        : manifest.Runtime!.SilentArgs!;

    string runtimeFileName = GetFileNameFromUrl(runtimeUrl, "windowsdesktop-runtime-installer.exe");
    string runtimeInstallerPath = Path.Combine(Path.GetTempPath(), runtimeFileName);

    Console.WriteLine($"[Bootstrapper] WindowsDesktop-Runtime >= {minimumVersion} fehlt. Lade Runtime nach...");
    using (IDownloadProgressSink progressSink = CreateProgressSink(".NET Desktop Runtime wird heruntergeladen..."))
    {
        await DownloadFileAsync(http, runtimeUrl, runtimeInstallerPath, progressSink.Report);
        progressSink.Complete();
    }

    var runtimeProcess = Process.Start(new ProcessStartInfo
    {
        FileName = runtimeInstallerPath,
        Arguments = runtimeArgs,
        UseShellExecute = true
    });

    if (runtimeProcess == null)
    {
        throw new InvalidOperationException("Runtime-Installer konnte nicht gestartet werden.");
    }

    runtimeProcess.WaitForExit();
    if (runtimeProcess.ExitCode != 0)
    {
        throw new InvalidOperationException($"Runtime-Installer fehlgeschlagen (ExitCode {runtimeProcess.ExitCode}).");
    }

    if (!IsWindowsDesktopRuntimeInstalled(minimumVersion))
    {
        throw new InvalidOperationException("WindowsDesktop-Runtime wurde nach der Installation nicht erkannt.");
    }

    TryDeleteFileWithRetries(runtimeInstallerPath);

    Console.WriteLine("[Bootstrapper] Runtime erfolgreich installiert.");
}

static void TryDeleteFileWithRetries(string filePath)
{
    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
    {
        return;
    }

    for (int attempt = 0; attempt < 10; attempt++)
    {
        try
        {
            File.Delete(filePath);
            return;
        }
        catch
        {
            Thread.Sleep(500);
        }
    }

    Console.WriteLine($"[Bootstrapper] Hinweis: Datei konnte nicht gelöscht werden: {filePath}");
}

static bool IsWindowsDesktopRuntimeInstalled(string minimumVersion)
{
    Version minimum = ParseMinimumVersion(minimumVersion);
    return IsRuntimeAvailableViaDotnet(minimum) || IsRuntimeAvailableViaRegistry(minimum);
}

static Version ParseMinimumVersion(string value)
{
    if (Version.TryParse(value, out var parsed) && parsed != null)
    {
        return parsed;
    }

    return new Version(10, 0, 0, 0);
}

static bool IsRuntimeAvailableViaDotnet(Version minimum)
{
    try
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "--list-runtimes",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null)
        {
            return false;
        }

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return false;
        }

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (!line.StartsWith("Microsoft.WindowsDesktop.App ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            if (!Version.TryParse(parts[1], out var runtimeVersion) || runtimeVersion == null)
            {
                continue;
            }

            if (runtimeVersion >= minimum)
            {
                return true;
            }
        }
    }
    catch
    {
    }

    return false;
}

static bool IsRuntimeAvailableViaRegistry(Version minimum)
{
    try
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App");
        if (key == null)
        {
            return false;
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            if (!Version.TryParse(subKeyName, out var runtimeVersion) || runtimeVersion == null)
            {
                continue;
            }

            if (runtimeVersion >= minimum)
            {
                return true;
            }
        }
    }
    catch
    {
    }

    return false;
}

static void ValidateSha256(string filePath, string expectedHash)
{
    string expected = NormalizeSha256(expectedHash);
    using var stream = File.OpenRead(filePath);
    var bytes = SHA256.HashData(stream);
    string actual = Convert.ToHexString(bytes).ToLowerInvariant();

    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("SHA-256 Prüfsumme stimmt nicht überein.");
    }
}

static string NormalizeSha256(string hash)
{
    return hash.Trim().Replace(" ", string.Empty).ToLowerInvariant();
}

static bool TryParseVersion(string? value, out Version version)
{
    version = new Version(0, 0, 0, 0);
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var clean = value.Trim();
    int plusIndex = clean.IndexOf('+');
    if (plusIndex >= 0)
    {
        clean = clean[..plusIndex];
    }

    int dashIndex = clean.IndexOf('-');
    if (dashIndex >= 0)
    {
        clean = clean[..dashIndex];
    }

    if (!Version.TryParse(clean, out var parsed) || parsed == null)
    {
        return false;
    }

    version = parsed;
    return true;
}

static string GetFileNameFromUrl(string url, string fallback)
{
    try
    {
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
    }
    catch
    {
        return fallback;
    }
}

static string? GetArgumentValue(string[] inputArgs, string key)
{
    for (int index = 0; index < inputArgs.Length; index++)
    {
        if (string.Equals(inputArgs[index], key, StringComparison.OrdinalIgnoreCase) && index + 1 < inputArgs.Length)
        {
            return inputArgs[index + 1];
        }
    }

    return null;
}

internal sealed class InstallerManifest
{
    public BootstrapperManifest? Bootstrapper { get; set; }
    public InstallerManifestEntry? Installer { get; set; }
    public RuntimeManifestEntry? Runtime { get; set; }
    public GitHubManifestEntry? GitHub { get; set; }
}

internal sealed class BootstrapperManifest
{
    public string Version { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Sha256 { get; set; }
}

internal sealed class InstallerManifestEntry
{
    public string? Url { get; set; }
    public string? Sha256 { get; set; }
    public string? SilentArgs { get; set; }
}

internal sealed class RuntimeManifestEntry
{
    public bool Enabled { get; set; } = true;
    public string? MinVersion { get; set; }
    public string? Url { get; set; }
    public string? SilentArgs { get; set; }
}

internal sealed class GitHubManifestEntry
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string? InstallerAssetName { get; set; }
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(InstallerManifest))]
[JsonSerializable(typeof(GitHubRelease))]
internal partial class BootstrapperJsonContext : JsonSerializerContext
{
}

internal readonly record struct InstallerPackage(string Url, string FileName, string? Sha256, string? SilentArgs);

internal readonly record struct DownloadProgress(long DownloadedBytes, long? TotalBytes)
{
    public int? Percent => TotalBytes is > 0
        ? (int)Math.Min(100, (DownloadedBytes * 100d) / TotalBytes.Value)
        : null;
}

internal static partial class NativeMessageBox
{
    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    public static extern int Show(nint hWnd, string text, string caption, uint type);

    public static int Show(string text, string caption, uint type)
    {
        return Show(0, text, caption, type);
    }
}

internal interface IDownloadProgressSink : IDisposable
{
    void Report(DownloadProgress progress);
    void Complete();
}

internal sealed class ExternalProgressWindowRenderer : IDownloadProgressSink
{
    private readonly object _sync = new();
    private readonly string _stateFilePath;
    private readonly string _scriptFilePath;
    private readonly Process _uiProcess;
    private bool _completed;
    private bool _disposed;
    private int _lastPercent = -1;

    public ExternalProgressWindowRenderer(string title)
    {
        _stateFilePath = Path.Combine(Path.GetTempPath(), $"pcwaechter-progress-{Guid.NewGuid():N}.state");
        _scriptFilePath = Path.Combine(Path.GetTempPath(), $"pcwaechter-progress-{Guid.NewGuid():N}.ps1");

        File.WriteAllText(_stateFilePath, "progress|0|Download wird vorbereitet...", new UTF8Encoding(false));
        File.WriteAllText(_scriptFilePath, GetProgressScriptContent(), new UTF8Encoding(false));

        string escapedStatePath = EscapePowerShellSingleQuoted(_stateFilePath);
        string escapedScriptPath = EscapePowerShellSingleQuoted(_scriptFilePath);
        string escapedTitle = EscapePowerShellSingleQuoted(title);

        _uiProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File '{escapedScriptPath}' -StateFile '{escapedStatePath}' -Title '{escapedTitle}'",
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Externes Fortschrittsfenster konnte nicht gestartet werden.");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (!_completed)
                {
                    SafeWriteState("done");
                }

                if (!_uiProcess.HasExited)
                {
                    _uiProcess.WaitForExit(700);
                }

                if (!_uiProcess.HasExited)
                {
                    _uiProcess.Kill(true);
                }
            }
            catch
            {
            }

            try { _uiProcess.Dispose(); } catch { }
            try { if (File.Exists(_stateFilePath)) File.Delete(_stateFilePath); } catch { }
            try { if (File.Exists(_scriptFilePath)) File.Delete(_scriptFilePath); } catch { }
        }
    }

    public void Report(DownloadProgress progress)
    {
        lock (_sync)
        {
            if (_disposed || _completed)
            {
                return;
            }

            int percent = Math.Clamp(progress.Percent ?? 0, 0, 100);
            if (percent == _lastPercent && progress.TotalBytes is not null)
            {
                return;
            }

            _lastPercent = percent;

            string text = progress.TotalBytes is > 0
                ? $"{percent}%  {FormatSize(progress.DownloadedBytes)} / {FormatSize(progress.TotalBytes.Value)}"
                : $"{FormatSize(progress.DownloadedBytes)} heruntergeladen";

            string payload = $"progress|{percent}|{SanitizeStateText(text)}";
            SafeWriteState(payload);
        }
    }

    public void Complete()
    {
        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            SafeWriteState("progress|100|Fertig");
            Thread.Sleep(250);
            SafeWriteState("done");
        }

        Dispose();
    }

    private void SafeWriteState(string payload)
    {
        File.WriteAllText(_stateFilePath, payload, new UTF8Encoding(false));
    }

    private static string SanitizeStateText(string text)
    {
        return text.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
    }

    private static string EscapePowerShellSingleQuoted(string value)
    {
        return value.Replace("'", "''");
    }

    private string GetProgressScriptContent()
    {
        return """
param(
    [Parameter(Mandatory = $true)]
    [string]$StateFile,

    [Parameter(Mandatory = $true)]
    [string]$Title
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$form = New-Object System.Windows.Forms.Form
$form.Text = "PC Wächter Installer"
$form.StartPosition = 'CenterScreen'
$form.Width = 560
$form.Height = 160
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.ControlBox = $false
$form.TopMost = $true

$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Left = 16
$titleLabel.Top = 14
$titleLabel.Width = 520
$titleLabel.Height = 32
$titleLabel.Text = $Title

$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Left = 16
$progressBar.Top = 54
$progressBar.Width = 520
$progressBar.Height = 22
$progressBar.Minimum = 0
$progressBar.Maximum = 100
$progressBar.Value = 0

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Left = 16
$statusLabel.Top = 84
$statusLabel.Width = 520
$statusLabel.Height = 24
$statusLabel.Text = 'Download wird vorbereitet...'

$form.Controls.Add($titleLabel)
$form.Controls.Add($progressBar)
$form.Controls.Add($statusLabel)

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 150
$timer.Add_Tick({
    if (-not (Test-Path $StateFile)) {
        return
    }

    $content = Get-Content -Path $StateFile -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($content)) {
        return
    }

    if ($content -eq 'done') {
        $form.Close()
        return
    }

    $parts = $content -split '\|', 3
    if ($parts.Length -lt 3) {
        return
    }

    if ($parts[0] -ne 'progress') {
        return
    }

    [int]$percent = 0
    [void][int]::TryParse($parts[1], [ref]$percent)
    if ($percent -lt 0) { $percent = 0 }
    if ($percent -gt 100) { $percent = 100 }

    $progressBar.Value = $percent
    $statusLabel.Text = $parts[2]
})

$timer.Start()
[void]$form.ShowDialog()
""";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}

internal sealed class ShellDownloadProgressRenderer : IDownloadProgressSink
{
    private const uint CoinitApartmentThreaded = 0x2;
    private const uint ClsctxInprocServer = 0x1;
    private static readonly Guid ProgressDialogClsid = new("F8383852-FCD3-11d1-A6B9-006097DF5BD4");
    private static readonly Guid ProgressDialogIid = new("EBBC7C04-315E-11d2-B62F-006097DF5BD4");

    private readonly Thread _uiThread;
    private readonly AutoResetEvent _queueSignal = new(false);
    private readonly ManualResetEventSlim _readySignal = new(false);
    private readonly ConcurrentQueue<UiAction> _uiQueue = new();

    private IProgressDialog? _dialog;
    private bool _completed;
    private bool _disposed;
    private bool _shutdownRequested;
    private bool _comInitialized;
    private Exception? _initializationError;

    public ShellDownloadProgressRenderer(string title)
    {
        _uiThread = new Thread(() => RunUiThread(title))
        {
            IsBackground = true,
            Name = "PCWaechterShellProgressDialog"
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();

        if (!_readySignal.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException("Download-Fortschrittsfenster konnte nicht initialisiert werden.");
        }

        if (_initializationError != null)
        {
            throw new InvalidOperationException("Download-Fortschrittsfenster konnte nicht initialisiert werden.", _initializationError);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        InvokeOnUi(() =>
        {
            _shutdownRequested = true;
            try
            {
                _dialog?.StopProgressDialog();
            }
            catch
            {
            }
        }, waitForCompletion: true);

        _queueSignal.Set();
        _uiThread.Join(TimeSpan.FromSeconds(2));
        _queueSignal.Dispose();
        _readySignal.Dispose();
    }

    public void Report(DownloadProgress progress)
    {
        if (_disposed || _completed)
        {
            return;
        }

        InvokeOnUi(() =>
        {
            if (_dialog == null)
            {
                return;
            }

            if (progress.TotalBytes is > 0)
            {
                _dialog.SetProgress64((ulong)progress.DownloadedBytes, (ulong)progress.TotalBytes.Value);
                int percent = Math.Clamp(progress.Percent ?? 0, 0, 100);
                _dialog.SetLine(2, $"{percent}%  {FormatSize(progress.DownloadedBytes)} / {FormatSize(progress.TotalBytes.Value)}", false, 0);
            }
            else
            {
                _dialog.SetLine(2, $"{FormatSize(progress.DownloadedBytes)} heruntergeladen", false, 0);
            }
        });
    }

    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        InvokeOnUi(() =>
        {
            if (_dialog == null)
            {
                return;
            }

            _dialog.SetLine(2, "Fertig", false, 0);
        }, waitForCompletion: true);

        Thread.Sleep(250);
        Dispose();
    }

    private void RunUiThread(string title)
    {
        try
        {
            int hr = CoInitializeEx(0, CoinitApartmentThreaded);
            if (hr >= 0)
            {
                _comInitialized = true;
            }

            _dialog = CreateProgressDialog();
            _dialog.SetTitle("PC Wächter Installer");
            _dialog.SetLine(1, title, false, 0);
            _dialog.SetLine(2, "Download wird vorbereitet...", false, 0);
            _dialog.StartProgressDialog(0, 0, ProgressDialogFlags.Normal | ProgressDialogFlags.NoMinimize, 0);
        }
        catch (Exception ex)
        {
            _initializationError = ex;
        }
        finally
        {
            _readySignal.Set();
        }

        while (!_shutdownRequested)
        {
            _queueSignal.WaitOne();

            while (_uiQueue.TryDequeue(out var action))
            {
                try
                {
                    action.Action();
                }
                catch
                {
                }
                finally
                {
                    action.DoneSignal?.Set();
                }
            }
        }

        try
        {
            if (_dialog != null)
            {
                Marshal.ReleaseComObject(_dialog);
                _dialog = null;
            }
        }
        catch
        {
        }

        if (_comInitialized)
        {
            try
            {
                CoUninitialize();
            }
            catch
            {
            }
        }
    }

    private void InvokeOnUi(Action action, bool waitForCompletion = false)
    {
        if (_disposed)
        {
            return;
        }

        ManualResetEventSlim? doneSignal = null;
        if (waitForCompletion)
        {
            doneSignal = new ManualResetEventSlim(false);
        }

        _uiQueue.Enqueue(new UiAction(action, doneSignal));
        _queueSignal.Set();

        if (doneSignal != null)
        {
            doneSignal.Wait(TimeSpan.FromSeconds(5));
            doneSignal.Dispose();
        }
    }

    private readonly record struct UiAction(Action Action, ManualResetEventSlim? DoneSignal);

    private static IProgressDialog CreateProgressDialog()
    {
        int hr = CoCreateInstance(in ProgressDialogClsid, 0, ClsctxInprocServer, in ProgressDialogIid, out nint instancePtr);
        if (hr < 0 || instancePtr == 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            return (IProgressDialog)Marshal.GetObjectForIUnknown(instancePtr);
        }
        finally
        {
            Marshal.Release(instancePtr);
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);
}

[Flags]
internal enum ProgressDialogFlags : uint
{
    Normal = 0x00000000,
    Modal = 0x00000001,
    AutoTime = 0x00000002,
    NoTime = 0x00000004,
    NoMinimize = 0x00000008
}

[ComImport]
[Guid("EBBC7C04-315E-11d2-B62F-006097DF5BD4")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IProgressDialog
{
    void StartProgressDialog(nint hwndParent, nint punkEnableModless, ProgressDialogFlags flags, nint pvResevered);
    void StopProgressDialog();
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pwzTitle);
    void SetAnimation(nint hInstAnimation, ushort idAnimation);

    [return: MarshalAs(UnmanagedType.Bool)]
    bool HasUserCancelled();

    void SetProgress(uint dwCompleted, uint dwTotal);
    void SetProgress64(ulong ullCompleted, ulong ullTotal);
    void SetLine(uint dwLineNum, [MarshalAs(UnmanagedType.LPWStr)] string pwzString, [MarshalAs(UnmanagedType.Bool)] bool fCompactPath, nint pvResevered);
    void SetCancelMsg([MarshalAs(UnmanagedType.LPWStr)] string pwzCancelMsg, nint pvResevered);
    void Timer(uint dwAction, nint pvResevered);
}

internal sealed class ConsoleDownloadProgressRenderer : IDownloadProgressSink
{
    private const int BarWidth = 32;
    private int _lastPercent = -1;
    private bool _completed;

    public ConsoleDownloadProgressRenderer(string title)
    {
        Console.WriteLine(title);
    }

    public void Dispose()
    {
    }

    public void Report(DownloadProgress progress)
    {
        if (_completed)
        {
            return;
        }

        int percent = progress.Percent ?? 0;
        if (percent == _lastPercent && progress.TotalBytes is not null)
        {
            return;
        }

        _lastPercent = percent;
        string bar = BuildBar(percent);
        string rightText = progress.TotalBytes is > 0
            ? $"{FormatSize(progress.DownloadedBytes)} / {FormatSize(progress.TotalBytes.Value)}"
            : $"{FormatSize(progress.DownloadedBytes)}";

        Console.Write($"\r{bar} {percent,3}%  {rightText}   ");
    }

    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        Console.Write($"\r{BuildBar(100)} 100%  Fertig{" ",8}\n");
    }

    private static string BuildBar(int percent)
    {
        int filled = (int)Math.Round(BarWidth * percent / 100d);
        return "[" + new string('█', filled) + new string('░', Math.Max(0, BarWidth - filled)) + "]";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
