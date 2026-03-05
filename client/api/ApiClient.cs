using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(string baseUrl, string? agentApiKey = null)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };

        if (!string.IsNullOrWhiteSpace(agentApiKey))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Agent-Api-Key", agentApiKey);
        }
    }

    public async Task RegisterAsync(Guid installId)
    {
        var payload = new
        {
            device_install_id = installId,
            hostname = Environment.MachineName,
            os = new { name = "Windows", version = Environment.OSVersion.VersionString, build = "" },
            agent = new { version = "0.1.0", channel = "stable" },
            network = new { primary_ip = "", macs = Array.Empty<string>() }
        };

        var resp = await _http.PostAsJsonAsync("agent/register", payload);
        resp.EnsureSuccessStatusCode();
    }

    public async Task HeartbeatAsync(Guid installId)
    {
        var payload = new
        {
            device_install_id = installId,
            at = DateTime.UtcNow,
            status = new
            {
                uptime_seconds = Environment.TickCount64 / 1000,
                logged_in_user = Environment.UserName
            }
        };

        var resp = await _http.PostAsJsonAsync("agent/heartbeat", payload);
        resp.EnsureSuccessStatusCode();
    }

    public async Task InventoryAsync(Guid installId)
    {
        var payload = new
        {
            device_install_id = installId,
            collected_at = DateTime.UtcNow,
            inventory = new
            {
                hardware = new { cpu = "", ram_gb = 0 },
                software = new[] { new { name = ".NET", version = Environment.Version.ToString() } }
            }
        };

        var resp = await _http.PostAsJsonAsync("agent/inventory", payload);
        resp.EnsureSuccessStatusCode();
    }
}
