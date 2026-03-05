using System.Management;
using AgentService.Runtime;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class PerformanceWatchSensor : ISensor
{
    public const string Id = "sensor.performance_watch";

    public string SensorId => Id;

    public Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = new PerformanceWatchSensorData
            {
                CpuPercent = ReadCpuPercent(),
                MemoryPercent = ReadMemoryPercent()
            };

            (string processName, int processCpu, int? processId) = ReadTopProcessCpu();
            data.TopProcessName = processName;
            data.TopProcessCpuPercent = processCpu;
            data.TopProcessId = processId;

            return Task.FromResult(new SensorResult
            {
                SensorId = Id,
                Success = true,
                Payload = data
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new SensorResult
            {
                SensorId = Id,
                Success = false,
                Error = ex.Message
            });
        }
    }

    private static int ReadCpuPercent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            int sum = 0;
            int count = 0;
            foreach (ManagementObject row in searcher.Get())
            {
                int load = ConvertToInt(row["LoadPercentage"]);
                if (load < 0)
                {
                    continue;
                }

                sum += load;
                count++;
            }

            if (count <= 0)
            {
                return 0;
            }

            return Math.Clamp((int)Math.Round(sum / (double)count), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static int ReadMemoryPercent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject row in searcher.Get())
            {
                double totalKb = Convert.ToDouble(row["TotalVisibleMemorySize"] ?? 0d);
                double freeKb = Convert.ToDouble(row["FreePhysicalMemory"] ?? 0d);
                if (totalKb <= 0)
                {
                    return 0;
                }

                double usedPercent = ((totalKb - freeKb) / totalKb) * 100d;
                return Math.Clamp((int)Math.Round(usedPercent), 0, 100);
            }
        }
        catch
        {
        }

        return 0;
    }

    private static (string ProcessName, int CpuPercent, int? ProcessId) ReadTopProcessCpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name,IDProcess,PercentProcessorTime FROM Win32_PerfFormattedData_PerfProc_Process");

            string topName = string.Empty;
            int topCpu = 0;
            int? topPid = null;

            foreach (ManagementObject row in searcher.Get())
            {
                string name = row["Name"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) ||
                    name.Equals("_Total", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Idle", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int cpu = ConvertToInt(row["PercentProcessorTime"]);
                if (cpu <= topCpu)
                {
                    continue;
                }

                topCpu = cpu;
                topName = name;
                topPid = ConvertToInt(row["IDProcess"]);
            }

            return (topName, Math.Max(0, topCpu), topPid);
        }
        catch
        {
            return (string.Empty, 0, null);
        }
    }

    private static int ConvertToInt(object? raw)
    {
        if (raw is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(raw);
        }
        catch
        {
            return 0;
        }
    }
}
