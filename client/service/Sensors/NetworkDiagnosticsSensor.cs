using System.Net.NetworkInformation;
using Microsoft.Win32;
using AgentService.Runtime;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class NetworkDiagnosticsSensor : ISensor
{
    public const string Id = "sensor.network_diagnostics";

    private const string InternetSettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public string SensorId => Id;

    public async Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = new NetworkDiagnosticsSensorData
            {
                HasInternet = NetworkInterface.GetIsNetworkAvailable(),
                ProxyEnabled = ReadProxyEnabled()
            };

            List<NetworkInterface> upInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(i => i.OperationalStatus == OperationalStatus.Up &&
                            i.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            i.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            data.AdapterSummary = upInterfaces.Count == 0
                ? "Keine aktiven Adapter"
                : $"{upInterfaces.Count} aktive Adapter";

            string? gateway = upInterfaces
                .SelectMany(i => i.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address?.ToString())
                .FirstOrDefault(g => !string.IsNullOrWhiteSpace(g) && g != "0.0.0.0");

            if (!string.IsNullOrWhiteSpace(gateway))
            {
                int? gatewayLatency = await PingAsync(gateway, cancellationToken);
                data.GatewayLatencyMs = gatewayLatency;
                data.GatewayReachable = gatewayLatency.HasValue;
            }

            int? publicDnsLatency = await PingAsync("8.8.8.8", cancellationToken);
            data.PublicDnsLatencyMs = publicDnsLatency;
            data.PublicDnsReachable = publicDnsLatency.HasValue;

            return new SensorResult
            {
                SensorId = Id,
                Success = true,
                Payload = data
            };
        }
        catch (Exception ex)
        {
            return new SensorResult
            {
                SensorId = Id,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static bool ReadProxyEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(InternetSettingsPath, false);
        object? raw = key?.GetValue("ProxyEnable");
        if (raw is null)
        {
            return false;
        }

        try
        {
            return Convert.ToInt32(raw) == 1;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<int?> PingAsync(string host, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        try
        {
            PingReply reply = await ping.SendPingAsync(host, 1200);
            if (reply.Status != IPStatus.Success)
            {
                return null;
            }

            return (int)reply.RoundtripTime;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}
