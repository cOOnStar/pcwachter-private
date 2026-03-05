using System.Text.Json;
using System.IO;

namespace PCWachter.Desktop.Services;

public sealed class AppUiStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _statePath;

    public AppUiStateStore()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCWaechter");
        _statePath = Path.Combine(root, "desktop-ui-state.json");
    }

    public AppUiState Load()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return new AppUiState();
            }

            string json = File.ReadAllText(_statePath);
            AppUiState? state = JsonSerializer.Deserialize<AppUiState>(json, JsonOptions);
            return state ?? new AppUiState();
        }
        catch
        {
            return new AppUiState();
        }
    }

    public void Save(AppUiState state)
    {
        string? directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_statePath, json);
    }
}

public sealed class AppUiState
{
    public double WindowWidth { get; set; } = 1420;
    public double WindowHeight { get; set; } = 890;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowMaximized { get; set; }
    public string LastNavKey { get; set; } = "Dashboard";
    public bool IsDemoMode { get; set; } = true;
    public bool IsAutomaticUpdatesEnabled { get; set; } = true;
    public string? PendingUpdateVersion { get; set; }
    public DateTimeOffset? PendingUpdateDetectedAtUtc { get; set; }
    public List<HealthScoreHistoryPoint> HealthScoreHistory { get; set; } = new();
}

public sealed class HealthScoreHistoryPoint
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Score { get; set; }
}
