using System.Management;
using PCWachter.Contracts;

namespace AgentService.Runtime;

internal sealed class DeviceContextProvider
{
    private static readonly HashSet<ushort> PortableChassisTypes =
    [
        8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32
    ];

    private readonly ILogger<DeviceContextProvider> _logger;

    public DeviceContextProvider(ILogger<DeviceContextProvider> logger)
    {
        _logger = logger;
    }

    public DeviceContextDto GetCurrentContext()
    {
        string fakeContext = Environment.GetEnvironmentVariable("PCWACHTER_FAKE_CONTEXT")?.Trim().ToUpperInvariant() ?? string.Empty;
        if (fakeContext == "LAPTOP" || fakeContext == "DESKTOP" || fakeContext == "SERVER")
        {
            return BuildFakeContext(fakeContext);
        }

        var context = new DeviceContextDto
        {
            OsVersion = Environment.OSVersion.VersionString
        };

        bool? batteryPresent = null;
        bool chassisSuggestsLaptop = false;

        try
        {
            using var csSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\CIMV2",
                "SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem");

            foreach (ManagementObject row in csSearcher.Get())
            {
                context.Manufacturer = row["Manufacturer"]?.ToString();
                context.Model = row["Model"]?.ToString();

                if (row["TotalPhysicalMemory"] is not null)
                {
                    long totalBytes = Convert.ToInt64(row["TotalPhysicalMemory"]);
                    context.MemoryGb = (int)Math.Round(totalBytes / 1024d / 1024d / 1024d);
                }

                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read Win32_ComputerSystem");
        }

        try
        {
            using var cpuSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\CIMV2",
                "SELECT Name FROM Win32_Processor");

            foreach (ManagementObject row in cpuSearcher.Get())
            {
                context.Cpu = row["Name"]?.ToString();
                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read Win32_Processor");
        }

        try
        {
            using var batterySearcher = new ManagementObjectSearcher(
                @"\\localhost\root\CIMV2",
                "SELECT Name FROM Win32_Battery");

            batteryPresent = batterySearcher.Get().Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read Win32_Battery");
        }

        try
        {
            using var enclosureSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\CIMV2",
                "SELECT ChassisTypes FROM Win32_SystemEnclosure");

            foreach (ManagementObject row in enclosureSearcher.Get())
            {
                if (row["ChassisTypes"] is ushort[] chassisValues)
                {
                    if (chassisValues.Any(value => PortableChassisTypes.Contains(value)))
                    {
                        chassisSuggestsLaptop = true;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read Win32_SystemEnclosure");
        }

        bool isServer = DetectServerHeuristic(context);
        bool isLaptop = !isServer && (batteryPresent == true || chassisSuggestsLaptop);
        bool isDesktop = !isServer && !isLaptop;

        context.IsLaptop = isLaptop;
        context.IsDesktop = isDesktop;
        context.IsServer = isServer;

        return context;
    }

    private static DeviceContextDto BuildFakeContext(string fake)
    {
        bool isLaptop = fake == "LAPTOP";
        bool isDesktop = fake == "DESKTOP";
        bool isServer = fake == "SERVER";

        return new DeviceContextDto
        {
            IsLaptop = isLaptop,
            IsDesktop = isDesktop,
            IsServer = isServer,
            Manufacturer = "FAKE",
            Model = $"FAKE-{fake}",
            OsVersion = Environment.OSVersion.VersionString,
            MemoryGb = null,
            Cpu = null
        };
    }

    private static bool DetectServerHeuristic(DeviceContextDto context)
    {
        string combined = string.Join(' ',
            context.Model ?? string.Empty,
            context.Manufacturer ?? string.Empty,
            context.OsVersion ?? string.Empty);

        if (combined.Contains("server", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var osSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\CIMV2",
                "SELECT ProductType FROM Win32_OperatingSystem");

            foreach (ManagementObject row in osSearcher.Get())
            {
                if (row["ProductType"] is not null)
                {
                    // 1 = Workstation, 2 = Domain Controller, 3 = Server
                    uint productType = Convert.ToUInt32(row["ProductType"]);
                    return productType == 2 || productType == 3;
                }
            }
        }
        catch
        {
        }

        return false;
    }
}
