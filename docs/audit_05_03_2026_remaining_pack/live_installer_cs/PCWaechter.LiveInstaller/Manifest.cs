using System.Text.Json.Serialization;

namespace PCWaechter.LiveInstaller;

public sealed class InstallerManifest
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; } = 1;
    [JsonPropertyName("channel")] public string Channel { get; set; } = "stable";
    [JsonPropertyName("version")] public string Version { get; set; } = "0.0.0";
    [JsonPropertyName("released_at")] public string? ReleasedAt { get; set; }

    [JsonPropertyName("min_supported_version")] public string? MinSupportedVersion { get; set; }
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }

    [JsonPropertyName("offline")] public OfflineInstaller Offline { get; set; } = new();
}

public sealed class OfflineInstaller
{
    [JsonPropertyName("name")] public string Name { get; set; } = "PCWaechter_offline_installer.exe";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    [JsonPropertyName("size_bytes")] public long SizeBytes { get; set; }
    [JsonPropertyName("signature_required")] public bool SignatureRequired { get; set; } = false;
    [JsonPropertyName("signature_subject_allowlist")] public string[] SignatureSubjectAllowlist { get; set; } = Array.Empty<string>();
}
