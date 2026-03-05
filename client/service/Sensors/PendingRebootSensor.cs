using AgentService.Runtime;
using Microsoft.Win32;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class PendingRebootSensor : ISensor
{
    public const string Id = "sensor.pending_reboot";

    public string SensorId => Id;

    public Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var triggers = new List<string>();

            if (Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") is not null)
            {
                triggers.Add("windows_update_reboot_required");
            }

            if (Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") is not null)
            {
                triggers.Add("cbs_reboot_pending");
            }

            using (var sessionManager = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
            {
                object? value = sessionManager?.GetValue("PendingFileRenameOperations");
                if (value is string[] values && values.Length > 0)
                {
                    triggers.Add("pending_file_rename_operations");
                }
            }

            var payload = new PendingRebootSensorData
            {
                IsPending = triggers.Count > 0,
                TriggeredSignals = triggers
            };

            return Task.FromResult(new SensorResult
            {
                SensorId = Id,
                Success = true,
                Payload = payload
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
}
