using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using PCWachter.Contracts;

namespace PCWachter.Desktop.Services;

/// <summary>
/// Checks license status against the PCWächter backend API.
/// Caches the result for 5 minutes to avoid excessive API calls.
/// </summary>
public sealed class LicenseService
{
    private const string ApiBaseUrl = "https://api.xn--pcwchter-2za.de";
    private static readonly HttpClient _http = new();
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly KeycloakAuthService _auth;
    private LicenseStatusDto? _cached;
    private DateTime _cacheExpiresAt = DateTime.MinValue;

    public LicenseService(KeycloakAuthService auth)
    {
        _auth = auth;
    }

    public LicenseStatusDto? CurrentLicense => _cached;

    public event EventHandler? LicenseChanged;

    /// <summary>Check license status for a given install ID (from agent identity).</summary>
    public async Task<LicenseStatusDto> CheckLicenseAsync(Guid installId, CancellationToken ct = default)
    {
        if (_cached is not null && DateTime.UtcNow < _cacheExpiresAt)
            return _cached;

        string? token = await _auth.GetValidTokenAsync(ct).ConfigureAwait(false);

        string url = $"{ApiBaseUrl}/license/status?device_install_id={Uri.EscapeDataString(installId.ToString())}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        // Use Bearer token if available, otherwise API key (for agent)
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            HttpResponseMessage res = await _http.SendAsync(req, ct).ConfigureAwait(false);

            if (res.IsSuccessStatusCode)
            {
                string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                LicenseStatusDto? dto = JsonSerializer.Deserialize<LicenseStatusDto>(json, _jsonOpts);
                if (dto is not null)
                {
                    _cached = dto;
                    _cacheExpiresAt = DateTime.UtcNow.AddMinutes(5);
                    LicenseChanged?.Invoke(this, EventArgs.Empty);
                    return dto;
                }
            }
        }
        catch { /* network error – return cached or empty */ }

        return _cached ?? new LicenseStatusDto();
    }

    /// <summary>Activate a license key on this device.</summary>
    public async Task<(bool Success, string Error)> ActivateLicenseAsync(
        string licenseKey,
        Guid installId,
        string? keycloakUserId = null,
        CancellationToken ct = default)
    {
        string? token = await _auth.GetValidTokenAsync(ct).ConfigureAwait(false);

        string url = $"{ApiBaseUrl}/license/activate";
        var body = JsonSerializer.Serialize(new
        {
            license_key = licenseKey.Trim().ToUpperInvariant(),
            device_install_id = installId.ToString(),
            keycloak_user_id = keycloakUserId,
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            HttpResponseMessage res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (res.IsSuccessStatusCode)
            {
                // Invalidate cache so next check picks up the activated license
                _cached = null;
                _cacheExpiresAt = DateTime.MinValue;
                LicenseChanged?.Invoke(this, EventArgs.Empty);
                return (true, string.Empty);
            }

            // Try to extract error detail
            try
            {
                using var doc = JsonDocument.Parse(json);
                string? detail = doc.RootElement.TryGetProperty("detail", out JsonElement d) ? d.GetString() : null;
                return (false, detail ?? $"HTTP {res.StatusCode}");
            }
            catch
            {
                return (false, $"HTTP {res.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public void Invalidate()
    {
        _cached = null;
        _cacheExpiresAt = DateTime.MinValue;
    }
}
