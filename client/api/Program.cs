using System;
using System.Threading.Tasks;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var baseUrl = args.Length > 0 ? args[0] : "https://api.pcwächter.de";
        var agentApiKey = args.Length > 1
            ? args[1]
            : Environment.GetEnvironmentVariable("PCWAECHTER_AGENT_API_KEY");

        var installId = AgentIdentity.GetOrCreateInstallId();
        Console.WriteLine($"device_install_id: {installId}");

        var api = new ApiClient(baseUrl, agentApiKey);

        await api.RegisterAsync(installId);
        await api.HeartbeatAsync(installId);
        await api.InventoryAsync(installId);

        Console.WriteLine("Sent register + heartbeat + inventory OK");
    }
}
