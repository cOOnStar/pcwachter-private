using PCWachter.Contracts;

namespace PCWachter.Desktop.Services;

public static class DemoReportFactory
{
    public static ScanReportDto Create(DateTimeOffset nowUtc)
    {
        DateTimeOffset baseTime = nowUtc.AddHours(-8);

        FindingDto defenderRealtimeOff = CreateFinding(
            "security.defender.realtime_off",
            "rule.defender",
            FindingCategory.Security,
            FindingSeverity.Critical,
            97,
            "Defender Echtzeitschutz deaktiviert",
            "Der Echtzeitschutz war zuletzt ausgeschaltet.",
            baseTime.AddMinutes(10),
            activeDays: 1,
            isNew: true,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = "demo",
                ["realtime_protection"] = "off"
            },
            actions:
            [
                RunRemediationAction("action.defender.enable_realtime", "Echtzeitschutz aktivieren", "remediation.defender.enable_realtime", requiresAdmin: true)
            ]);

        FindingDto firewallWarning = CreateFinding(
            "security.firewall.profile_public_off",
            "rule.firewall",
            FindingCategory.Security,
            FindingSeverity.Warning,
            84,
            "Firewall-Profil öffentlich deaktiviert",
            "Das öffentliche Netzwerkprofil ist nicht aktiv.",
            baseTime.AddMinutes(20),
            activeDays: 2,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["disabled_profiles"] = "Public"
            },
            actions:
            [
                RunRemediationAction("action.firewall.enable_all", "Firewall für alle Profile aktivieren", "remediation.firewall.enable_all", requiresAdmin: true)
            ]);

        FindingDto bitLockerWarning = CreateFinding(
            "security.bitlocker.system_off",
            "rule.bitlocker",
            FindingCategory.Security,
            FindingSeverity.Warning,
            76,
            "BitLocker auf Systemlaufwerk aus",
            "Systemlaufwerk C: ist aktuell nicht geschützt.",
            baseTime.AddMinutes(25),
            activeDays: 4,
            actions:
            [
                OpenExternalAction("action.security.open_bitlocker", "BitLocker-Einstellungen öffnen", "ms-settings:deviceencryption")
            ]);

        FindingDto storageLow = CreateFinding(
            "storage.system.low_space",
            "rule.storage",
            FindingCategory.Storage,
            FindingSeverity.Warning,
            80,
            "Wenig freier Speicher auf C:",
            "Nur noch 11.8% freier Speicher auf dem Systemlaufwerk.",
            baseTime.AddMinutes(35),
            activeDays: 3,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["system_drive"] = "C:",
                ["free_bytes"] = "95200000000",
                ["total_bytes"] = "805000000000",
                ["percent_free"] = "11.80"
            },
            actions:
            [
                OpenExternalAction("action.storage.open_cleanup", "Speicherbereinigung öffnen", "ms-settings:storagesense")
            ]);

        FindingDto storageBreakdown = CreateFinding(
            "storage.analysis.breakdown",
            "rule.storage",
            FindingCategory.Storage,
            FindingSeverity.Info,
            54,
            "Was frisst Platz",
            "Breakdown für Downloads, Temp, Update-Cache und Papierkorb.",
            baseTime.AddMinutes(36),
            activeDays: 1,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["downloads_bytes"] = "12600000000",
                ["temp_bytes"] = "2350000000",
                ["windows_update_cache_bytes"] = "4380000000",
                ["recycle_bin_bytes"] = "870000000",
                ["top_consumers"] = "Downloads:12600000000|Windows Update Cache:4380000000|Temp:2350000000|Papierkorb:870000000"
            });

        FindingDto storageSmartCritical = CreateFinding(
            "storage.disk.health.critical.c",
            "rule.storage",
            FindingCategory.Storage,
            FindingSeverity.Critical,
            92,
            "SMART-Alarm auf Laufwerk C:",
            "SMART meldet ein mögliches Laufwerksproblem.",
            baseTime.AddMinutes(37),
            activeDays: 1,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["drive"] = "C:",
                ["predict_failure"] = "true"
            });

        FindingDto rebootPending = CreateFinding(
            "system.reboot.pending",
            "rule.pending_reboot",
            FindingCategory.System,
            FindingSeverity.Warning,
            72,
            "Neustart ausstehend",
            "Ein Neustart ist für abgeschlossene Updates erforderlich.",
            baseTime.AddMinutes(40),
            activeDays: 1);

        FindingDto eventlogErrors = CreateFinding(
            "health.eventlog.error_spike",
            "rule.eventlog",
            FindingCategory.Health,
            FindingSeverity.Warning,
            70,
            "Systemfehler in den letzten 24h",
            "Mehrere Fehler im System- und Anwendungsprotokoll erkannt.",
            baseTime.AddMinutes(42),
            activeDays: 2,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["system_error_count_24h"] = "9",
                ["application_error_count_24h"] = "6"
            });

        FindingDto startupDiscord = CreateFinding(
            "system.startup.app.discord",
            "rule.startup",
            FindingCategory.System,
            FindingSeverity.Warning,
            66,
            "Autostart: Discord (High Impact)",
            "Der Eintrag verlängert den Systemstart.",
            baseTime.AddMinutes(45),
            activeDays: 5,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["entry_key"] = "discord",
                ["name"] = "Discord",
                ["command"] = "\"C:\\Program Files\\Discord\\Update.exe\" --processStart Discord.exe",
                ["location"] = "HKCU_RUN",
                ["impact"] = "High",
                ["disabled_by_pcwachter"] = "false"
            },
            actions:
            [
                RunRemediationAction("action.startup.disable", "Autostart deaktivieren", "remediation.startup.disable")
            ]);

        FindingDto startupSteam = CreateFinding(
            "system.startup.app.steam",
            "rule.startup",
            FindingCategory.System,
            FindingSeverity.Warning,
            61,
            "Autostart: Steam (High Impact)",
            "Steam startet bei jeder Anmeldung automatisch.",
            baseTime.AddMinutes(47),
            activeDays: 6,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["entry_key"] = "steam",
                ["name"] = "Steam",
                ["command"] = "\"C:\\Program Files (x86)\\Steam\\steam.exe\" -silent",
                ["location"] = "HKCU_RUN",
                ["impact"] = "High",
                ["disabled_by_pcwachter"] = "false"
            },
            actions:
            [
                RunRemediationAction("action.startup.disable", "Autostart deaktivieren", "remediation.startup.disable")
            ]);

        FindingDto startupOneDriveDisabled = CreateFinding(
            "system.startup.app.onedrive",
            "rule.startup",
            FindingCategory.System,
            FindingSeverity.Info,
            35,
            "Autostart: OneDrive deaktiviert",
            "Eintrag wurde zuvor von PCWachter deaktiviert.",
            baseTime.AddMinutes(48),
            activeDays: 1,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["entry_key"] = "onedrive",
                ["name"] = "OneDrive",
                ["command"] = "\"C:\\Program Files\\Microsoft OneDrive\\OneDrive.exe\" /background",
                ["location"] = "HKCU_RUN",
                ["impact"] = "Medium",
                ["disabled_by_pcwachter"] = "true"
            },
            actions:
            [
                RunRemediationAction("action.startup.undo", "Rückgängig", "remediation.startup.undo")
            ]);

        FindingDto networkSummary = CreateFinding(
            "network.diagnostics.summary",
            "rule.network",
            FindingCategory.System,
            FindingSeverity.Warning,
            64,
            "Netzwerkdiagnose mit Hinweisen",
            "Internet erreichbar, aber Latenz und DNS sollten optimiert werden.",
            baseTime.AddMinutes(52),
            activeDays: 1,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["adapter_summary"] = "LAN: Up | WLAN: Up (78%)",
                ["gateway_latency_ms"] = "22",
                ["public_dns_latency_ms"] = "37",
                ["proxy_enabled"] = "false",
                ["has_internet"] = "true"
            },
            actions:
            [
                RunRemediationAction("action.network.flush_dns", "DNS Cache leeren", "remediation.network.flush_dns"),
                RunRemediationAction("action.network.disable_proxy", "Proxy deaktivieren", "remediation.network.disable_proxy")
            ]);

        FindingDto appsOutdatedCount = CreateFinding(
            "apps.outdated.count",
            "rule.app_updates",
            FindingCategory.System,
            FindingSeverity.Info,
            58,
            "3 Programme veraltet",
            "Mehrere installierte Anwendungen haben verfügbare Updates.",
            baseTime.AddMinutes(55),
            activeDays: 2,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["count"] = "3",
                ["winget_version"] = "1.9.25200"
            });

        FindingDto appChrome = CreateFinding(
            "apps.outdated.googlechrome",
            "rule.app_updates",
            FindingCategory.System,
            FindingSeverity.Warning,
            65,
            "Google Chrome veraltet",
            "Sicherheitsrelevantes Browser-Update verfügbar.",
            baseTime.AddMinutes(56),
            activeDays: 3,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package_id"] = "Google.Chrome",
                ["name"] = "Google Chrome",
                ["installed_version"] = "133.0.6943.54",
                ["available_version"] = "134.0.7012.16",
                ["source"] = "winget",
                ["download_size"] = "112 MB"
            });

        FindingDto appVlc = CreateFinding(
            "apps.outdated.vlc",
            "rule.app_updates",
            FindingCategory.System,
            FindingSeverity.Info,
            49,
            "VLC Media Player veraltet",
            "Ein Funktions- und Stabilitätsupdate ist verfügbar.",
            baseTime.AddMinutes(57),
            activeDays: 7,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package_id"] = "VideoLAN.VLC",
                ["name"] = "VLC media player",
                ["installed_version"] = "3.0.20",
                ["available_version"] = "3.0.21",
                ["source"] = "winget",
                ["download_size"] = "46 MB"
            });

        FindingDto app7Zip = CreateFinding(
            "apps.outdated.7zip",
            "rule.app_updates",
            FindingCategory.System,
            FindingSeverity.Info,
            47,
            "7-Zip veraltet",
            "Ein aktuelles Wartungsrelease ist verfügbar.",
            baseTime.AddMinutes(58),
            activeDays: 5,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package_id"] = "7zip.7zip",
                ["name"] = "7-Zip",
                ["installed_version"] = "24.06",
                ["available_version"] = "24.09",
                ["source"] = "winget",
                ["download_size"] = "2.2 MB"
            });

        FindingDto updatesSecurity = CreateFinding(
            "updates.security.missing",
            "rule.windows_updates",
            FindingCategory.System,
            FindingSeverity.Critical,
            90,
            "Kritische Sicherheitsupdates ausstehend",
            "Es fehlen wichtige Windows-Sicherheitsupdates.",
            baseTime.AddMinutes(59),
            activeDays: 1,
            actions:
            [
                RunRemediationAction("action.updates.install_security", "Sicherheitsupdates installieren", "remediation.updates.install_security", mayRestart: true)
            ]);

        FindingDto updatesOptional = CreateFinding(
            "updates.optional.available",
            "rule.windows_updates",
            FindingCategory.System,
            FindingSeverity.Warning,
            55,
            "Optionale Windows-Updates verfügbar",
            "Zusatzupdates können Kompatibilitaet und Stabilität verbessern.",
            baseTime.AddMinutes(60),
            activeDays: 3,
            actions:
            [
                RunRemediationAction("action.updates.install_optional", "Optionale Updates installieren", "remediation.updates.install_optional")
            ]);

        FindingDto updatesDrivers = CreateFinding(
            "updates.drivers.available",
            "rule.windows_updates",
            FindingCategory.System,
            FindingSeverity.Info,
            40,
            "Treiberupdates verfügbar",
            "Neue Treiberversionen wurden erkannt.",
            baseTime.AddMinutes(62),
            activeDays: 4,
            actions:
            [
                OpenExternalAction("action.updates.open_driver_updates", "Treiberseite öffnen", "ms-settings:windowsupdate-optionalupdates")
            ]);

        FindingDto performanceSpikes = CreateFinding(
            "health.performance.spikes_today",
            "rule.performance_watch",
            FindingCategory.Health,
            FindingSeverity.Info,
            52,
            "Performance-Spitzen erkannt",
            "Heute wurden mehrere Lastspitzen erkannt.",
            baseTime.AddMinutes(65),
            activeDays: 1,
            evidence: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["spikes_today"] = "3",
                ["latest_process"] = "MsMpEng.exe",
                ["latest_value"] = "94"
            });

        var findings = new List<FindingDto>
        {
            defenderRealtimeOff,
            firewallWarning,
            bitLockerWarning,
            storageLow,
            storageBreakdown,
            storageSmartCritical,
            rebootPending,
            eventlogErrors,
            startupDiscord,
            startupSteam,
            startupOneDriveDisabled,
            networkSummary,
            appsOutdatedCount,
            appChrome,
            appVlc,
            app7Zip,
            updatesSecurity,
            updatesOptional,
            updatesDrivers,
            performanceSpikes
        };

        FindingDto resolvedFirewall = CreateFinding(
            "security.firewall.profile_domain_off",
            "rule.firewall",
            FindingCategory.Security,
            FindingSeverity.Warning,
            79,
            "Firewall-Domainprofil war deaktiviert",
            "Das Profil wurde wieder aktiviert.",
            nowUtc.AddDays(-1),
            activeDays: 0,
            isResolvedRecently: true);
        resolvedFirewall.State.ResolvedAtUtc = nowUtc.AddMinutes(-22);
        resolvedFirewall.State.LastSeenUtc = nowUtc.AddMinutes(-24);

        FindingDto resolvedTemp = CreateFinding(
            "storage.cleanup.temp",
            "rule.storage",
            FindingCategory.Storage,
            FindingSeverity.Info,
            42,
            "Temporare Dateien bereinigt",
            "Temp-Daten wurden erfolgreich entfernt.",
            nowUtc.AddDays(-1),
            activeDays: 0,
            isResolvedRecently: true);
        resolvedTemp.State.ResolvedAtUtc = nowUtc.AddMinutes(-12);
        resolvedTemp.State.LastSeenUtc = nowUtc.AddMinutes(-14);

        var top = findings
            .OrderByDescending(f => f.Priority)
            .Take(3)
            .ToList();

        return new ScanReportDto
        {
            GeneratedAtUtc = nowUtc,
            OverallStatus = "warning",
            HealthScore = 74,
            DeviceContext = new DeviceContextDto
            {
                IsLaptop = true,
                Manufacturer = "HP",
                Model = "EliteBook 850 G7",
                OsVersion = "Windows 11 Pro 24H2",
                MemoryGb = 16,
                Cpu = "Intel Core i7"
            },
            Findings = findings,
            TopFindings = top,
            RecentlyResolved = [resolvedFirewall, resolvedTemp],
            AutoFixPolicy = new AutoFixPolicyDto
            {
                Mode = AutoFixMode.RecommendOnly,
                RequireNetwork = true,
                RequireAcPower = false,
                MaxFixesPerDay = 6,
                CooldownHours = 2,
                ScanIntervalMinutes = 30
            },
            RecentAutoFixLog =
            [
                new AutoFixLogItemDto
                {
                    TimestampUtc = nowUtc.AddMinutes(-12),
                    FindingId = resolvedTemp.FindingId,
                    ActionId = "action.storage.open_cleanup",
                    ActionExecutionId = "mockexec001",
                    RollbackAvailable = false,
                    Success = true,
                    Message = "Sichere Bereinigung erfolgreich simuliert."
                },
                new AutoFixLogItemDto
                {
                    TimestampUtc = nowUtc.AddMinutes(-22),
                    FindingId = resolvedFirewall.FindingId,
                    ActionId = "action.firewall.enable_all",
                    ActionExecutionId = "mockexec000",
                    RollbackAvailable = true,
                    RollbackHint = "Rollback im Mockup-Modus verfügbar.",
                    Success = true,
                    Message = "Firewall-Profile wurden simuliert aktiviert."
                }
            ],
            Timeline =
            [
                new TimelineEventDto
                {
                    TimestampUtc = nowUtc.AddMinutes(-12),
                    Kind = "action_succeeded",
                    Level = "success",
                    Title = "Aktion erfolgreich",
                    Message = "Temporare Dateien wurden bereinigt.",
                    FindingId = resolvedTemp.FindingId,
                    ActionId = "action.storage.open_cleanup",
                    ActionExecutionId = "mockexec001"
                },
                new TimelineEventDto
                {
                    TimestampUtc = nowUtc.AddMinutes(-22),
                    Kind = "action_succeeded",
                    Level = "success",
                    Title = "Aktion erfolgreich",
                    Message = "Firewallprofile wurden aktiviert.",
                    FindingId = resolvedFirewall.FindingId,
                    ActionId = "action.firewall.enable_all",
                    ActionExecutionId = "mockexec000"
                },
                new TimelineEventDto
                {
                    TimestampUtc = nowUtc.AddMinutes(-50),
                    Kind = "scan_completed",
                    Level = "info",
                    Title = "Scan abgeschlossen",
                    Message = "Mockup-Scan wurde aktualisiert."
                }
            ],
            BaselineDrift = new BaselineDriftSummaryDto
            {
                HasBaseline = true,
                BaselineCreatedAtUtc = nowUtc.AddDays(-7),
                BaselineLabel = "Demo-Baseline",
                NewFindings = 2,
                ChangedFindings = 1,
                ResolvedFindings = 3
            },
            RuleThresholds = new RuleThresholdsDto
            {
                StorageCriticalPercentFree = 5,
                StorageWarningPercentFree = 15,
                EventLogWarningCount24h = 10,
                DefenderSignatureWarningDays = 7,
                DefenderSignatureCriticalDays = 14
            },
            SecuritySignals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["smartscreen"] = "on",
                ["tamper_protection"] = "on",
                ["controlled_folder_access"] = "warn",
                ["exploit_protection"] = "on",
                ["lsa_protection"] = "on",
                ["credential_guard"] = "off",
                ["monthly_summary"] = "Du bist diesen Monat sicherer geworden (+9 Punkte).",
                ["monthly_score_delta"] = "9",
                ["monthly_resolved_count"] = "6",
                ["monthly_open_count"] = "4",
                ["monthly_trend_points"] = "62,64,66,67,69,70,72,73,74"
            },
            Drives =
            [
                new DriveStatusDto
                {
                    Name = "C:",
                    TotalBytes = 805000000000,
                    FreeBytes = 95000000000,
                    HealthState = DriveHealthState.Critical,
                    HealthBadgeText = "Kritisch",
                    SmartAvailable = true,
                    PredictFailure = true,
                    TemperatureC = 66,
                    HealthDetails =
                    [
                        "SMART meldet ein mögliches Laufwerksproblem.",
                        "SMART-Temperatur kritisch (66°C).",
                        "Speicher wird knapp (11.8% frei)."
                    ],
                    LastCheckedUtc = nowUtc.AddMinutes(-2)
                },
                new DriveStatusDto
                {
                    Name = "D:",
                    TotalBytes = 1000000000000,
                    FreeBytes = 720000000000,
                    HealthState = DriveHealthState.Good,
                    HealthBadgeText = "Zustand gut",
                    SmartAvailable = true,
                    PredictFailure = false,
                    TemperatureC = 38,
                    LastCheckedUtc = nowUtc.AddMinutes(-2)
                },
                new DriveStatusDto
                {
                    Name = "E:",
                    TotalBytes = 500000000000,
                    FreeBytes = 68000000000,
                    HealthState = DriveHealthState.Warning,
                    HealthBadgeText = "Achtung",
                    SmartAvailable = false,
                    PredictFailure = false,
                    HealthDetails =
                    [
                        "SMART ist fur dieses Laufwerk nicht verfugbar.",
                        "Speicher wird knapp (13.6% frei)."
                    ],
                    LastCheckedUtc = nowUtc.AddMinutes(-2)
                }
            ]
        };
    }

    private static FindingDto CreateFinding(
        string id,
        string ruleId,
        FindingCategory category,
        FindingSeverity severity,
        int priority,
        string title,
        string summary,
        DateTimeOffset detectedAtUtc,
        int activeDays,
        bool isNew = false,
        bool isResolvedRecently = false,
        Dictionary<string, string>? evidence = null,
        List<ActionDto>? actions = null)
    {
        var finding = new FindingDto
        {
            FindingId = id,
            RuleId = ruleId,
            Category = category,
            Severity = severity,
            Priority = priority,
            Title = title,
            Summary = summary,
            DetectedAtUtc = detectedAtUtc,
            Evidence = evidence ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            State = new FindingStateDto
            {
                IsNew = isNew,
                ActiveDays = activeDays,
                ActiveStreakScans = Math.Max(1, activeDays),
                FirstSeenUtc = detectedAtUtc.AddDays(-Math.Max(0, activeDays - 1)),
                LastSeenUtc = detectedAtUtc.AddMinutes(5),
                IsResolvedRecently = isResolvedRecently
            }
        };

        if (actions is not null)
        {
            finding.Actions.AddRange(actions);
        }

        return finding;
    }

    private static ActionDto RunRemediationAction(string id, string label, string remediationId, bool requiresAdmin = false, bool mayRestart = false)
    {
        return new ActionDto
        {
            ActionId = id,
            Label = label,
            Kind = ActionKind.RunRemediation,
            RemediationId = remediationId,
            IsSafeForOneClickMaintenance = !requiresAdmin,
            RequiresAdmin = requiresAdmin,
            MayRequireRestart = mayRestart
        };
    }

    private static ActionDto OpenExternalAction(string id, string label, string target)
    {
        return new ActionDto
        {
            ActionId = id,
            Label = label,
            Kind = ActionKind.OpenExternal,
            ExternalTarget = target,
            IsSafeForOneClickMaintenance = true
        };
    }
}



