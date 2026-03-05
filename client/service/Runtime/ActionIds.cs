namespace AgentService.Runtime;

internal static class ActionIds
{
    public const string DefenderUpdateSignatures = "action.defender.update_signatures";
    public const string DefenderEnableRealtime = "action.defender.enable_realtime";
    public const string StorageOpenCleanup = "action.storage.open_cleanup";
    public const string BitLockerHowTo = "action.security.bitlocker.howto";
    public const string SystemRebootNow = "action.system.reboot_now";
    public const string FirewallEnableAll = "action.security.firewall.enable_all";
    public const string EventLogOpenViewer = "action.health.eventlog.open_viewer";
    public const string AppsUpdateSelected = "action.apps.update_selected";
    public const string StartupDisable = "action.startup.disable";
    public const string StartupUndo = "action.startup.undo";
    public const string NetworkFlushDns = "action.network.flush_dns";
    public const string NetworkDisableProxy = "action.network.disable_proxy";
    public const string NetworkResetAdapters = "action.network.reset_adapters";
    public const string WindowsInstallAllUpdates = "action.updates.install_all_windows";
    public const string WindowsInstallSecurityUpdates = "action.updates.install_security";
    public const string WindowsInstallOptionalUpdates = "action.updates.install_optional";
    public const string WindowsOpenDriverUpdates = "action.updates.open_driver_updates";
}
