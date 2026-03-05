using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCWachter.Contracts;

namespace PCWachter.Desktop.Services;

/// <summary>
/// Implements Keycloak PKCE Authorization Code Flow.
/// Opens the system browser, starts a local HTTP listener to receive the callback,
/// exchanges the code for tokens, and persists them on disk.
/// </summary>
public sealed class KeycloakAuthService
{
    private const string RedirectUri = "http://localhost:8765/callback";
    private const string TokenCachePath = @"PCWächter\tokens.json";

    private static readonly HttpClient _http = new();
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _keycloakUrl;
    private readonly string _realm;
    private readonly string _clientId;

    private TokenCache? _tokens;

    public KeycloakAuthService(string keycloakUrl, string realm, string clientId)
    {
        _keycloakUrl = keycloakUrl.TrimEnd('/');
        _realm = realm;
        _clientId = clientId;
        LoadTokensFromDisk();
    }

    public bool IsAuthenticated => _tokens is not null && !string.IsNullOrEmpty(_tokens.AccessToken);
    public string? AccessToken => _tokens?.AccessToken;
    public string? UserSub => _tokens?.Sub;

    public event EventHandler? AuthStateChanged;

    // ── Public API ────────────────────────────────────────────────────────

    public async Task<bool> LoginAsync(CancellationToken ct = default)
    {
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);
        string state = GenerateState();

        string authUrl = BuildAuthUrl(codeChallenge, state);

        // Open browser
        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

        // Listen for callback
        string? code = await ListenForCallbackAsync(state, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(code)) return false;

        // Exchange code for tokens
        bool ok = await ExchangeCodeAsync(code, codeVerifier, ct).ConfigureAwait(false);
        if (ok)
        {
            SaveTokensToDisk();
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
        }
        return ok;
    }

    public async Task<string?> GetValidTokenAsync(CancellationToken ct = default)
    {
        if (_tokens is null) return null;
        if (IsTokenExpired(_tokens))
        {
            bool refreshed = await TryRefreshAsync(ct).ConfigureAwait(false);
            if (!refreshed) return null;
        }
        return _tokens?.AccessToken;
    }

    public async Task<KeycloakUserInfo?> GetUserInfoAsync(CancellationToken ct = default)
    {
        string? token = await GetValidTokenAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token)) return null;

        string userInfoUrl = $"{_keycloakUrl}/realms/{_realm}/protocol/openid-connect/userinfo";
        using var req = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            HttpResponseMessage res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;
            string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<KeycloakUserInfo>(json, _jsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        if (_tokens?.RefreshToken is { } refreshToken)
        {
            string logoutUrl = $"{_keycloakUrl}/realms/{_realm}/protocol/openid-connect/logout";
            var form = new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["refresh_token"] = refreshToken,
            };
            try
            {
                await _http.PostAsync(logoutUrl, new FormUrlEncodedContent(form)).ConfigureAwait(false);
            }
            catch { /* ignore */ }
        }

        _tokens = null;
        DeleteTokensFromDisk();
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private string BuildAuthUrl(string codeChallenge, string state)
    {
        string baseUrl = $"{_keycloakUrl}/realms/{_realm}/protocol/openid-connect/auth";
        var q = new StringBuilder();
        q.Append($"client_id={Uri.EscapeDataString(_clientId)}");
        q.Append($"&redirect_uri={Uri.EscapeDataString(RedirectUri)}");
        q.Append("&response_type=code");
        q.Append("&scope=openid%20email%20profile");
        q.Append($"&state={Uri.EscapeDataString(state)}");
        q.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}");
        q.Append("&code_challenge_method=S256");
        return $"{baseUrl}?{q}";
    }

    private static async Task<string?> ListenForCallbackAsync(string expectedState, CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8765/");
        listener.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            var context = await listener.GetContextAsync().WaitAsync(cts.Token).ConfigureAwait(false);
            HttpListenerRequest request = context.Request;
            string? code = request.QueryString["code"];
            string? state = request.QueryString["state"];
            string? error = request.QueryString["error"];

            // Respond to browser
            string html = error is not null
                ? "<html><body><h2>Login fehlgeschlagen.</h2><script>window.close();</script></body></html>"
                : "<html><body><h2>Login erfolgreich! Du kannst diesen Tab schließen.</h2><script>window.close();</script></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.OutputStream.WriteAsync(buffer, cts.Token).ConfigureAwait(false);
            context.Response.Close();

            if (error is not null || state != expectedState || string.IsNullOrEmpty(code))
                return null;

            return code;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task<bool> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
    {
        string tokenUrl = $"{_keycloakUrl}/realms/{_realm}/protocol/openid-connect/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _clientId,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = codeVerifier,
        };

        try
        {
            HttpResponseMessage res = await _http.PostAsync(tokenUrl, new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return false;

            string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var raw = JsonSerializer.Deserialize<TokenResponse>(json, _jsonOpts);
            if (raw?.AccessToken is null) return false;

            _tokens = new TokenCache
            {
                AccessToken = raw.AccessToken,
                RefreshToken = raw.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(raw.ExpiresIn - 30),
                Sub = ExtractSub(raw.AccessToken),
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        if (_tokens?.RefreshToken is null) return false;

        string tokenUrl = $"{_keycloakUrl}/realms/{_realm}/protocol/openid-connect/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _clientId,
            ["refresh_token"] = _tokens.RefreshToken,
        };

        try
        {
            HttpResponseMessage res = await _http.PostAsync(tokenUrl, new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                _tokens = null;
                DeleteTokensFromDisk();
                AuthStateChanged?.Invoke(this, EventArgs.Empty);
                return false;
            }

            string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var raw = JsonSerializer.Deserialize<TokenResponse>(json, _jsonOpts);
            if (raw?.AccessToken is null) return false;

            _tokens = new TokenCache
            {
                AccessToken = raw.AccessToken,
                RefreshToken = raw.RefreshToken ?? _tokens.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(raw.ExpiresIn - 30),
                Sub = ExtractSub(raw.AccessToken),
            };
            SaveTokensToDisk();
            AuthStateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTokenExpired(TokenCache tokens)
        => DateTime.UtcNow >= tokens.ExpiresAt;

    private static string ExtractSub(string jwt)
    {
        try
        {
            string[] parts = jwt.Split('.');
            if (parts.Length < 2) return string.Empty;
            string payload = parts[1];
            payload += new string('=', (4 - payload.Length % 4) % 4);
            byte[] bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            using var doc = JsonDocument.Parse(bytes);
            return doc.RootElement.TryGetProperty("sub", out JsonElement sub) ? sub.GetString() ?? "" : "";
        }
        catch { return string.Empty; }
    }

    // ── PKCE helpers ──────────────────────────────────────────────────────

    private static string GenerateCodeVerifier()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateState()
    {
        byte[] bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ── Token persistence ─────────────────────────────────────────────────

    private string TokenFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        TokenCachePath);

    private void SaveTokensToDisk()
    {
        if (_tokens is null) return;
        try
        {
            string dir = Path.GetDirectoryName(TokenFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(TokenFilePath, JsonSerializer.Serialize(_tokens, _jsonOpts));
        }
        catch { /* ignore */ }
    }

    private void LoadTokensFromDisk()
    {
        try
        {
            if (!File.Exists(TokenFilePath)) return;
            string json = File.ReadAllText(TokenFilePath);
            _tokens = JsonSerializer.Deserialize<TokenCache>(json, _jsonOpts);
        }
        catch { _tokens = null; }
    }

    private void DeleteTokensFromDisk()
    {
        try { if (File.Exists(TokenFilePath)) File.Delete(TokenFilePath); } catch { }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]  public string? AccessToken  { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")]    public int ExpiresIn        { get; set; }
    }

    private sealed class TokenCache
    {
        public string AccessToken  { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt  { get; set; }
        public string Sub           { get; set; } = string.Empty;
    }
}
