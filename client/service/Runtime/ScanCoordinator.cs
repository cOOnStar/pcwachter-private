using System.Diagnostics;
using System.Text.Json;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Runtime;

internal sealed class ScanCoordinator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyList<ISensor> _sensors;
    private readonly IReadOnlyList<IRule> _rules;
    private readonly IReadOnlyDictionary<string, IRemediation> _remediations;
    private readonly IStateStore _stateStore;
    private readonly DeviceContextProvider _deviceContextProvider;
    private readonly UxIntelligenceOptions _uxOptions;
    private readonly ILogger<ScanCoordinator> _logger;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    private ScanReportDto _lastReport = new();

    public ScanCoordinator(
        IEnumerable<ISensor> sensors,
        IEnumerable<IRule> rules,
        IEnumerable<IRemediation> remediations,
        IStateStore stateStore,
        DeviceContextProvider deviceContextProvider,
        UxIntelligenceOptions uxOptions,
        ILogger<ScanCoordinator> logger)
    {
        _sensors = sensors.ToList();
        _rules = rules.ToList();
        _remediations = remediations.ToDictionary(x => x.RemediationId, x => x, StringComparer.OrdinalIgnoreCase);
        _stateStore = stateStore;
        _deviceContextProvider = deviceContextProvider;
        _uxOptions = uxOptions;
        _logger = logger;
    }

    public ScanReportDto GetCurrentReport() => _lastReport;

    public async Task<ScanReportDto> RunScanAsync(CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken);
        try
        {
            int sensorTimeoutSeconds = Math.Max(1, _uxOptions.SensorTimeoutSeconds);
            int slowSensorWarningMs = Math.Max(250, _uxOptions.SlowSensorWarningMs);
            Stopwatch scanStopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "Scan started. sensors={SensorCount}, sensorTimeout={SensorTimeoutSeconds}s, slowSensorWarning>{SlowSensorWarningMs}ms",
                _sensors.Count,
                sensorTimeoutSeconds,
                slowSensorWarningMs);

            var sensorResults = new Dictionary<string, SensorResult>(StringComparer.OrdinalIgnoreCase);
            var sensorErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (ISensor sensor in _sensors)
            {
                Stopwatch sensorStopwatch = Stopwatch.StartNew();
                SensorResult result;
                try
                {
                    result = await CollectSensorWithTimeoutAsync(sensor, sensorTimeoutSeconds, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = new SensorResult
                    {
                        SensorId = sensor.SensorId,
                        Success = false,
                        Error = ex.Message
                    };
                }
                finally
                {
                    sensorStopwatch.Stop();
                }

                long elapsedMs = sensorStopwatch.ElapsedMilliseconds;

                sensorResults[sensor.SensorId] = result;

                if (!result.Success)
                {
                    sensorErrors[sensor.SensorId] = result.Error ?? "sensor failed";
                    _logger.LogWarning(
                        "Sensor {SensorId} failed in {ElapsedMs}ms: {Error}",
                        sensor.SensorId,
                        elapsedMs,
                        result.Error ?? "sensor failed");
                    continue;
                }

                if (elapsedMs >= slowSensorWarningMs)
                {
                    _logger.LogWarning("Sensor {SensorId} was slow: {ElapsedMs}ms", sensor.SensorId, elapsedMs);
                }
                else
                {
                    _logger.LogInformation("Sensor {SensorId} completed in {ElapsedMs}ms", sensor.SensorId, elapsedMs);
                }
            }

            DeviceContextDto deviceContext = _deviceContextProvider.GetCurrentContext();
            var nowUtc = DateTimeOffset.UtcNow;
            ServiceStateDto state = await _stateStore.LoadAsync(cancellationToken);
            EnsureStateSchema(state);
            state.RuleThresholds.Normalize();

            var ruleContext = new RuleContext
            {
                NowUtc = nowUtc,
                MachineName = Environment.MachineName,
                DeviceContext = deviceContext,
                Thresholds = state.RuleThresholds
            };

            List<FindingDto> rawFindings = EvaluateRules(sensorResults, ruleContext);
            InjectDebugFindings(rawFindings, ruleContext);
            ApplyContextualRecommendations(rawFindings, deviceContext);
            EnrichCriticalityDetails(rawFindings);
            EnrichFindingGuidance(rawFindings);
            Dictionary<string, string> securitySignals = BuildSecuritySignals(sensorResults);
            UpdateCapabilitySignals(state, sensorResults, securitySignals);
            UpdatePerformanceSpikes(state, sensorResults, nowUtc);
            FindingDto? performanceSpikeFinding = BuildPerformanceSpikeSummaryFinding(state, nowUtc);
            if (performanceSpikeFinding is not null)
            {
                rawFindings.Add(performanceSpikeFinding);
            }

            BaselineDriftSummaryDto driftSummary = BuildBaselineDriftSummary(state, rawFindings);
            FindingDto? driftFinding = BuildBaselineDriftFinding(driftSummary, nowUtc);
            if (driftFinding is not null)
            {
                rawFindings.Add(driftFinding);
            }

            Dictionary<string, bool> changedMap = UpdateHistory(rawFindings, state, nowUtc);
            ApplyLifecycleState(rawFindings, state, nowUtc, changedMap);

            foreach (FindingDto finding in rawFindings)
            {
                bool changed = changedMap.TryGetValue(finding.FindingId, out bool marker) && marker;
                finding.Priority = PriorityCalculator.Calculate(finding, finding.State.ActiveDays, changed);
                if (state.History.TryGetValue(finding.FindingId, out FindingHistoryRecordDto? record))
                {
                    record.LastPriority = finding.Priority;
                    state.History[finding.FindingId] = record;
                }
            }

            List<FindingDto> activeFiltered = rawFindings
                .Where(f => !IsSuppressed(state, f.FindingId, nowUtc))
                .OrderByDescending(f => f.Priority)
                .ThenByDescending(f => f.Severity)
                .ThenByDescending(f => state.History.TryGetValue(f.FindingId, out FindingHistoryRecordDto? h) ? h.LastChangedUtc : DateTimeOffset.MinValue)
                .ThenBy(f => f.FindingId, StringComparer.OrdinalIgnoreCase)
                .Select(CloneFinding)
                .ToList();

            int topCount = Math.Max(1, _uxOptions.TopFindingsCount);
            List<FindingDto> topFindings = activeFiltered.Take(topCount).Select(CloneFinding).ToList();

            List<FindingDto> recentlyResolved = BuildRecentlyResolvedList(state, nowUtc);
            int healthScore = HealthScoreCalculator.Calculate(activeFiltered);

            UpdateDailyHealthSnapshots(state, nowUtc, healthScore, activeFiltered.Count, recentlyResolved.Count);
            EnrichMonthlyHealthSignals(state, securitySignals, nowUtc, activeFiltered.Count, recentlyResolved.Count);

            var report = new ScanReportDto
            {
                GeneratedAtUtc = nowUtc,
                DeviceContext = deviceContext,
                Findings = activeFiltered,
                TopFindings = topFindings,
                RecentlyResolved = recentlyResolved,
                RecentAutoFixLog = BuildAutoFixLog(state),
                Timeline = BuildTimeline(state),
                BaselineDrift = driftSummary,
                RuleThresholds = CloneThresholds(state.RuleThresholds),
                SensorErrors = sensorErrors,
                SecuritySignals = securitySignals,
                Drives = BuildDriveStatuses(sensorResults, nowUtc),
                HealthScore = healthScore,
                OverallStatus = ComputeOverallStatus(activeFiltered)
            };

            await _stateStore.SaveAsync(state, cancellationToken);
            RuntimePaths.EnsureDirectories();
            await WriteAtomicJsonAsync(RuntimePaths.ScanReportPath, report, cancellationToken);

            scanStopwatch.Stop();
            _logger.LogInformation(
                "Scan finished in {ElapsedMs}ms. findings={FindingCount}, sensorErrors={SensorErrorCount}, status={OverallStatus}",
                scanStopwatch.ElapsedMilliseconds,
                report.Findings.Count,
                report.SensorErrors.Count,
                report.OverallStatus);

            _lastReport = report;
            return report;
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private static async Task<SensorResult> CollectSensorWithTimeoutAsync(
        ISensor sensor,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            return await sensor.CollectAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new SensorResult
            {
                SensorId = sensor.SensorId,
                Success = false,
                Error = $"sensor timeout after {timeoutSeconds}s"
            };
        }
    }

    private static List<DriveStatusDto> BuildDriveStatuses(
        IReadOnlyDictionary<string, SensorResult> sensorResults,
        DateTimeOffset fallbackCheckedUtc)
    {
        if (!sensorResults.TryGetValue(StorageSensor.Id, out SensorResult? result)
            || !result.Success
            || result.Payload is not StorageSensorData storageData
            || storageData.Drives.Count == 0)
        {
            return [];
        }

        return storageData.Drives
            .Select(d => new DriveStatusDto
            {
                Name = d.Name,
                TotalBytes = d.TotalBytes,
                FreeBytes = d.FreeBytes,
                HealthState = d.HealthState,
                HealthBadgeText = string.IsNullOrWhiteSpace(d.HealthBadgeText) ? "Unbekannt" : d.HealthBadgeText,
                SmartAvailable = d.SmartAvailable,
                PredictFailure = d.PredictFailure,
                TemperatureC = d.TemperatureC,
                HealthDetails = d.HealthDetails.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                LastCheckedUtc = d.LastCheckedUtc == default ? fallbackCheckedUtc : d.LastCheckedUtc
            })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ActionExecutionResultDto> ExecuteActionAsync(
        string actionId,
        bool? simulationOverride,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return new ActionExecutionResultDto { Success = false, ExitCode = 10, Message = "ActionId fehlt." };
        }

        FindingDto? finding = _lastReport.Findings
            .FirstOrDefault(f => f.Actions.Any(a => a.ActionId.Equals(actionId, StringComparison.OrdinalIgnoreCase)));

        ActionDto? action = finding?.Actions.FirstOrDefault(a => a.ActionId.Equals(actionId, StringComparison.OrdinalIgnoreCase));
        if (action is null)
        {
            action = ResolveGlobalAction(actionId);
            finding = new FindingDto
            {
                FindingId = $"global.action.{actionId}",
                RuleId = "rule.global.actions",
                Category = FindingCategory.System,
                Severity = FindingSeverity.Info,
                Title = action?.Label ?? actionId,
                Summary = "Globale Service-Aktion.",
                DetectedAtUtc = DateTimeOffset.UtcNow
            };
        }

        if (action is null || finding is null)
        {
            return new ActionExecutionResultDto
            {
                Success = false,
                ExitCode = 10,
                Message = $"Action '{actionId}' nicht im aktuellen Report gefunden."
            };
        }

        if (action.Kind != ActionKind.RunRemediation || string.IsNullOrWhiteSpace(action.RemediationId))
        {
            return new ActionExecutionResultDto
            {
                ActionExecutionId = string.Empty,
                Success = true,
                ExitCode = 0,
                Message = "Action ist kein Service-Remediation-Task (wird im Desktop ausgefuehrt)."
            };
        }

        if (!_remediations.TryGetValue(action.RemediationId, out IRemediation? remediation))
        {
            return new ActionExecutionResultDto
            {
                ActionExecutionId = string.Empty,
                Success = false,
                ExitCode = 10,
                Message = $"Keine Remediation fuer '{action.RemediationId}' registriert."
            };
        }

        ServiceStateDto state = await _stateStore.LoadAsync(cancellationToken);
        EnsureStateSchema(state);
        bool simulation = simulationOverride ?? state.SimulationMode;
        string actionExecutionId = Guid.NewGuid().ToString("N");

        (string? rollbackCommand, string? rollbackHint) = BuildRollbackPlan(action, finding);
        var record = new ActionExecutionRecordDto
        {
            ActionExecutionId = actionExecutionId,
            StartedAtUtc = DateTimeOffset.UtcNow,
            ActionId = action.ActionId,
            FindingId = finding.FindingId,
            RemediationId = action.RemediationId,
            RollbackPowerShellCommand = rollbackCommand,
            RollbackHint = rollbackHint,
            RollbackAvailable = !string.IsNullOrWhiteSpace(rollbackCommand)
        };

        if (!simulation && ShouldCreateRestorePoint(action))
        {
            RestorePointResult restorePoint = await TryCreateRestorePointAsync(action.ActionId, cancellationToken);
            record.RestorePointAttempted = restorePoint.Attempted;
            record.RestorePointCreated = restorePoint.Created;
            record.RestorePointDescription = restorePoint.Description;
            if (!string.IsNullOrWhiteSpace(restorePoint.Message))
            {
                record.Message = restorePoint.Message;
            }
        }

        AddTimelineEvent(state, new TimelineEventDto
        {
            Kind = "action_started",
            Level = "info",
            Title = "Aktion gestartet",
            Message = $"Aktion '{action.ActionId}' wurde gestartet.",
            ActionId = action.ActionId,
            ActionExecutionId = actionExecutionId,
            FindingId = finding.FindingId
        });

        var progress = new Progress<ActionProgressDto>(p =>
        {
            _logger.LogInformation("Action {ActionId}: {Percent}% {Message}", p.ActionId, p.Percent, p.Message);
        });

        RemediationResult result = await remediation.ExecuteAsync(
            new RemediationRequest
            {
                SimulationMode = simulation,
                ActionId = action.ActionId,
                FindingId = finding.FindingId,
                ExternalTarget = action.ExternalTarget,
                Parameters = parameters is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)
            },
            progress,
            cancellationToken);

        record.Success = result.Success;
        record.ExitCode = result.ExitCode;
        record.FinishedAtUtc = DateTimeOffset.UtcNow;
        record.Message = result.Message;
        if (!result.Success)
        {
            record.RollbackAvailable = false;
            record.RollbackPowerShellCommand = null;
        }

        state.ActionExecutions.Insert(0, record);
        if (state.ActionExecutions.Count > 120)
        {
            state.ActionExecutions = state.ActionExecutions.Take(120).ToList();
        }

        AddTimelineEvent(state, new TimelineEventDto
        {
            Kind = result.Success ? "action_succeeded" : "action_failed",
            Level = result.Success ? "success" : "critical",
            Title = result.Success ? "Aktion erfolgreich" : "Aktion fehlgeschlagen",
            Message = result.Message,
            ActionId = action.ActionId,
            ActionExecutionId = actionExecutionId,
            FindingId = finding.FindingId
        });

        await _stateStore.SaveAsync(state, cancellationToken);

        return new ActionExecutionResultDto
        {
            ActionExecutionId = actionExecutionId,
            Success = result.Success,
            ExitCode = result.ExitCode,
            Message = result.Message,
            RestorePointAttempted = record.RestorePointAttempted,
            RestorePointCreated = record.RestorePointCreated,
            RestorePointDescription = record.RestorePointDescription,
            RollbackAvailable = record.RollbackAvailable,
            RollbackHint = record.RollbackHint
        };
    }

    public async Task SetFindingStateAsync(SetFindingStateRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FindingId))
        {
            return;
        }

        ServiceStateDto state = await _stateStore.LoadAsync(cancellationToken);
        EnsureStateSchema(state);

        if (!state.Findings.TryGetValue(request.FindingId, out FindingStateDto? entry))
        {
            entry = new FindingStateDto();
            state.Findings[request.FindingId] = entry;
        }

        bool previousIgnored = entry.IsIgnored;
        DateTimeOffset? previousSnooze = entry.SnoozedUntilUtc;
        entry.IsIgnored = request.Ignore;
        entry.SnoozedUntilUtc = request.SnoozeUntilUtc?.ToUniversalTime();

        if (previousIgnored != entry.IsIgnored)
        {
            AddTimelineEvent(state, new TimelineEventDto
            {
                Kind = entry.IsIgnored ? "finding_ignored" : "finding_unignored",
                Level = "info",
                Title = entry.IsIgnored ? "Problem ignoriert" : "Ignorieren aufgehoben",
                Message = entry.IsIgnored
                    ? $"'{request.FindingId}' wurde ignoriert."
                    : $"'{request.FindingId}' wird wieder aktiv beobachtet.",
                FindingId = request.FindingId
            });
        }

        if (entry.SnoozedUntilUtc.HasValue
            && (!previousSnooze.HasValue || previousSnooze.Value != entry.SnoozedUntilUtc.Value))
        {
            AddTimelineEvent(state, new TimelineEventDto
            {
                Kind = "finding_snoozed",
                Level = "info",
                Title = "Problem pausiert",
                Message = $"'{request.FindingId}' pausiert bis {entry.SnoozedUntilUtc.Value.ToLocalTime():g}.",
                FindingId = request.FindingId
            });
        }

        await _stateStore.SaveAsync(state, cancellationToken);
    }

    public async Task<MaintenanceRunResultDto> RunSafeMaintenanceAsync(CancellationToken cancellationToken)
    {
        var result = new MaintenanceRunResultDto
        {
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        await _scanLock.WaitAsync(cancellationToken);
        try
        {
            result.Steps.Add(await ExecuteMaintenanceRemediationStepAsync(
                "maintenance.defender.signatures",
                "Defender Signaturen aktualisieren",
                "Aktualisiert die lokalen Defender-Signaturen.",
                MaintenanceRiskLevel.AdminRequired,
                ActionIds.DefenderUpdateSignatures,
                "remediation.defender.update_signatures",
                cancellationToken));

            result.Steps.Add(await ExecuteMaintenanceRemediationStepAsync(
                "maintenance.defender.realtime",
                "Defender Echtzeitschutz aktivieren",
                "Aktiviert den Defender Echtzeitschutz, falls deaktiviert.",
                MaintenanceRiskLevel.AdminRequired,
                ActionIds.DefenderEnableRealtime,
                "remediation.defender.enable_realtime",
                cancellationToken));

            result.Steps.Add(await ExecuteMaintenanceRemediationStepAsync(
                "maintenance.firewall.enable_all",
                "Firewall aktivieren",
                "Aktiviert Domain-, Private- und Public-Profil.",
                MaintenanceRiskLevel.AdminRequired,
                ActionIds.FirewallEnableAll,
                "remediation.firewall.enable_all",
                cancellationToken));

            result.Steps.Add(await ExecuteWindowsUpdateMaintenanceStepAsync(cancellationToken));
            result.Steps.Add(await ExecuteSafeCleanupStepAsync(cancellationToken));

            bool pendingReboot = _lastReport.Findings.Any(f =>
                string.Equals(f.FindingId, "system.reboot.pending", StringComparison.OrdinalIgnoreCase));
            bool restartLikely = result.Steps.Any(s =>
                s.Success && s.RiskLevel == MaintenanceRiskLevel.RestartPossible);
            result.RestartRecommended = pendingReboot || restartLikely;
        }
        finally
        {
            _scanLock.Release();
        }

        int successCount = result.Steps.Count(x => x.Success);
        int failedCount = result.Steps.Count(x => !x.Success && !x.Skipped);
        int skippedCount = result.Steps.Count(x => x.Skipped);
        result.FinishedAtUtc = DateTimeOffset.UtcNow;
        result.Summary = $"Wartung abgeschlossen: {successCount} erfolgreich, {failedCount} fehlgeschlagen, {skippedCount} uebersprungen."
            + (result.RestartRecommended ? " Neustart wird empfohlen." : string.Empty);

        await RunScanAsync(cancellationToken);
        return result;
    }

    public async Task<FeatureConfigDto> GetFeatureConfigAsync(CancellationToken cancellationToken)
    {
        ServiceStateDto state = await _stateStore.LoadAsync(cancellationToken);
        EnsureStateSchema(state);

        return new FeatureConfigDto
        {
            RuleThresholds = CloneThresholds(state.RuleThresholds),
            Baseline = BuildBaselineDriftSummary(state, _lastReport.Findings)
        };
    }

    public async Task SetFeatureConfigAsync(SetFeatureConfigRequestDto request, CancellationToken cancellationToken)
    {
        ServiceStateDto state = await _stateStore.LoadAsync(cancellationToken);
        EnsureStateSchema(state);

        if (request.RuleThresholds is not null)
        {
            state.RuleThresholds = CloneThresholds(request.RuleThresholds);
            state.RuleThresholds.Normalize();

            AddTimelineEvent(state, new TimelineEventDto
            {
                Kind = "thresholds_updated",
                Level = "info",
                Title = "Schwellwerte aktualisiert",
                Message =
                    $"Storage kritisch<{state.RuleThresholds.StorageCriticalPercentFree}%, " +
                    $"Storage Warnung<{state.RuleThresholds.StorageWarningPercentFree}%, " +
                    $"Eventlog>{state.RuleThresholds.EventLogWarningCount24h}/24h.",
            });
        }

        await _stateStore.SaveAsync(state, cancellationToken);
    }

    public async Task<CreateBaselineResultDto> CreateBaselineAsync(CreateBaselineRequestDto request, CancellationToken cancellationToken)
    {
        ServiceStateDto state = await _stateStore.LoadAsync(cancellationToken);
        EnsureStateSchema(state);

        List<FindingDto> findings = _lastReport.Findings
            .Where(f => !f.FindingId.Equals("system.baseline.drift", StringComparison.OrdinalIgnoreCase))
            .Select(CloneFinding)
            .ToList();

        if (findings.Count == 0)
        {
            return new CreateBaselineResultDto
            {
                Success = false,
                Message = "Keine aktiven Findings verfuegbar. Fuehren Sie zuerst einen Scan aus.",
                Baseline = BuildBaselineDriftSummary(state, _lastReport.Findings)
            };
        }

        state.Baseline = new BaselineSnapshotDto
        {
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Label = string.IsNullOrWhiteSpace(request.Label) ? "Standard-Baseline" : request.Label.Trim(),
            Findings = findings.Select(f => new BaselineItemDto
            {
                FindingId = f.FindingId,
                Severity = f.Severity,
                EvidenceHash = FindingFingerprint.ComputeEvidenceHash(f)
            }).ToList()
        };

        AddTimelineEvent(state, new TimelineEventDto
        {
            Kind = "baseline_created",
            Level = "success",
            Title = "Baseline erstellt",
            Message = $"Baseline '{state.Baseline.Label}' mit {state.Baseline.Findings.Count} Eintraegen erstellt."
        });

        await _stateStore.SaveAsync(state, cancellationToken);

        return new CreateBaselineResultDto
        {
            Success = true,
            Message = "Baseline wurde gespeichert.",
            Baseline = BuildBaselineDriftSummary(state, _lastReport.Findings)
        };
    }

    public async Task<ActionExecutionResultDto> RollbackActionAsync(string actionExecutionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actionExecutionId))
        {
            return new ActionExecutionResultDto
            {
                ActionExecutionId = string.Empty,
                Success = false,
                ExitCode = 10,
                Message = "ActionExecutionId fehlt."
            };
        }

        ServiceStateDto state = await _stateStore.LoadAsync(cancellationToken);
        EnsureStateSchema(state);

        ActionExecutionRecordDto? record = state.ActionExecutions
            .FirstOrDefault(x => x.ActionExecutionId.Equals(actionExecutionId, StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            return new ActionExecutionResultDto
            {
                ActionExecutionId = actionExecutionId,
                Success = false,
                ExitCode = 10,
                Message = "Aktionseintrag fuer Rollback nicht gefunden."
            };
        }

        if (!record.RollbackAvailable || string.IsNullOrWhiteSpace(record.RollbackPowerShellCommand))
        {
            return new ActionExecutionResultDto
            {
                ActionExecutionId = actionExecutionId,
                Success = false,
                ExitCode = 10,
                Message = "Fuer diese Aktion ist kein automatisches Rollback verfuegbar."
            };
        }

        ProcessExecutionResult rollback = await PowerShellRunner.RunAsync(
            record.RollbackPowerShellCommand,
            TimeSpan.FromMinutes(2),
            cancellationToken);

        bool success = !rollback.TimedOut && rollback.ExitCode == 0;
        string message = success
            ? "Rollback erfolgreich ausgefuehrt."
            : BuildProcessErrorMessage("Rollback fehlgeschlagen.", rollback);

        record.RollbackExecuted = true;
        record.RollbackAtUtc = DateTimeOffset.UtcNow;
        record.RollbackSuccess = success;
        record.RollbackMessage = message;

        AddTimelineEvent(state, new TimelineEventDto
        {
            Kind = success ? "rollback_succeeded" : "rollback_failed",
            Level = success ? "success" : "critical",
            Title = success ? "Rollback erfolgreich" : "Rollback fehlgeschlagen",
            Message = message,
            ActionId = record.ActionId,
            ActionExecutionId = record.ActionExecutionId,
            FindingId = record.FindingId
        });

        await _stateStore.SaveAsync(state, cancellationToken);

        return new ActionExecutionResultDto
        {
            ActionExecutionId = actionExecutionId,
            Success = success,
            ExitCode = rollback.ExitCode,
            Message = message
        };
    }

    private List<FindingDto> EvaluateRules(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        foreach (IRule rule in _rules)
        {
            try
            {
                findings.AddRange(rule.Evaluate(sensorResults, context));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rule {RuleId} failed", rule.RuleId);
                findings.Add(new FindingDto
                {
                    FindingId = $"health.rule.{rule.RuleId}.failed",
                    RuleId = rule.RuleId,
                    Category = FindingCategory.Health,
                    Severity = FindingSeverity.Info,
                    Title = "Regel konnte nicht ausgewertet werden",
                    Summary = ex.Message,
                    DetectedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        return findings
            .GroupBy(f => f.FindingId, StringComparer.OrdinalIgnoreCase)
            .Select(ChoosePrimaryFinding)
            .ToList();
    }

    private static FindingDto ChoosePrimaryFinding(IGrouping<string, FindingDto> group)
    {
        return group
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.DetectedAtUtc)
            .ThenBy(f => f.RuleId, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static void EnsureStateSchema(ServiceStateDto state)
    {
        state.Findings ??= new Dictionary<string, FindingStateDto>(StringComparer.OrdinalIgnoreCase);
        state.History ??= new Dictionary<string, FindingHistoryRecordDto>(StringComparer.OrdinalIgnoreCase);
        state.ResolvedRecentlyQueue ??= new List<ResolvedFindingCacheItemDto>();
        state.RuleThresholds ??= new RuleThresholdsDto();
        state.RuleThresholds.Normalize();
        state.Timeline ??= new List<TimelineEventDto>();
        state.ActionExecutions ??= new List<ActionExecutionRecordDto>();
        state.StartupUndoEntries ??= new Dictionary<string, StartupUndoEntryDto>(StringComparer.OrdinalIgnoreCase);
        state.PerformanceSpikes ??= new List<PerformanceSpikeRecordDto>();
        state.DailyHealthSnapshots ??= new List<HealthScoreDailySnapshotDto>();
        state.Capabilities ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (state.Version <= 0)
        {
            state.Version = 1;
        }

        if (state.SchemaVersion <= 0)
        {
            state.SchemaVersion = 4;
        }
    }

    private static ActionDto? ResolveGlobalAction(string actionId)
    {
        return actionId switch
        {
            ActionIds.AppsUpdateSelected => new ActionDto
            {
                ActionId = ActionIds.AppsUpdateSelected,
                Label = "Ausgewaehlte Apps aktualisieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.AppsUpdateSelectedRemediation.Id,
                IsSafeForOneClickMaintenance = false,
                RequiresAdmin = true,
                MayRequireRestart = true
            },
            ActionIds.StartupDisable => new ActionDto
            {
                ActionId = ActionIds.StartupDisable,
                Label = "Autostart deaktivieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.StartupDisableRemediation.Id,
                IsSafeForOneClickMaintenance = false,
                RequiresAdmin = true,
                MayRequireRestart = false
            },
            ActionIds.StartupUndo => new ActionDto
            {
                ActionId = ActionIds.StartupUndo,
                Label = "Autostart wiederherstellen",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.StartupUndoRemediation.Id,
                IsSafeForOneClickMaintenance = true,
                RequiresAdmin = true,
                MayRequireRestart = false
            },
            ActionIds.NetworkFlushDns => new ActionDto
            {
                ActionId = ActionIds.NetworkFlushDns,
                Label = "DNS leeren",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.NetworkFlushDnsRemediation.Id,
                IsSafeForOneClickMaintenance = true,
                RequiresAdmin = true,
                MayRequireRestart = false
            },
            ActionIds.NetworkDisableProxy => new ActionDto
            {
                ActionId = ActionIds.NetworkDisableProxy,
                Label = "Proxy deaktivieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.NetworkDisableProxyRemediation.Id,
                IsSafeForOneClickMaintenance = false,
                RequiresAdmin = false,
                MayRequireRestart = false
            },
            ActionIds.NetworkResetAdapters => new ActionDto
            {
                ActionId = ActionIds.NetworkResetAdapters,
                Label = "Adapter resetten",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.NetworkResetAdaptersRemediation.Id,
                IsSafeForOneClickMaintenance = false,
                RequiresAdmin = true,
                MayRequireRestart = false
            },
            ActionIds.WindowsInstallAllUpdates => new ActionDto
            {
                ActionId = ActionIds.WindowsInstallAllUpdates,
                Label = "Windows Updates installieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.WindowsInstallAllUpdatesRemediation.Id,
                IsSafeForOneClickMaintenance = true,
                RequiresAdmin = true,
                MayRequireRestart = true
            },
            ActionIds.WindowsInstallSecurityUpdates => new ActionDto
            {
                ActionId = ActionIds.WindowsInstallSecurityUpdates,
                Label = "Sicherheitsupdates installieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.WindowsInstallSecurityUpdatesRemediation.Id,
                IsSafeForOneClickMaintenance = true,
                RequiresAdmin = true,
                MayRequireRestart = true
            },
            ActionIds.WindowsInstallOptionalUpdates => new ActionDto
            {
                ActionId = ActionIds.WindowsInstallOptionalUpdates,
                Label = "Optionale Updates installieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = Remediations.WindowsInstallOptionalUpdatesRemediation.Id,
                IsSafeForOneClickMaintenance = false,
                RequiresAdmin = true,
                MayRequireRestart = true
            },
            _ => null
        };
    }

    private static bool ShouldCreateRestorePoint(ActionDto action)
    {
        if (action.MayRequireRestart)
        {
            return true;
        }

        if (!action.IsSafeForOneClickMaintenance && action.RequiresAdmin)
        {
            return true;
        }

        return false;
    }

    private static void UpdateCapabilitySignals(
        ServiceStateDto state,
        IReadOnlyDictionary<string, SensorResult> sensorResults,
        Dictionary<string, string> securitySignals)
    {
        string wingetCapability = "missing";
        if (sensorResults.TryGetValue(Sensors.AppUpdatesSensor.Id, out SensorResult? appResult) &&
            appResult.Success &&
            appResult.Payload is AppUpdatesSensorData appData)
        {
            wingetCapability = appData.WingetAvailable ? "ok" : "missing";
        }

        state.Capabilities["winget"] = wingetCapability;
        state.Capabilities["windows_update_sensor"] = sensorResults.TryGetValue(Sensors.WindowsUpdatesSensor.Id, out SensorResult? wuResult) && wuResult.Success ? "ok" : "failed";
        state.Capabilities["startup_sensor"] = sensorResults.TryGetValue(Sensors.StartupAppsSensor.Id, out SensorResult? startupResult) && startupResult.Success ? "ok" : "failed";
        state.Capabilities["network_diagnostics_sensor"] = sensorResults.TryGetValue(Sensors.NetworkDiagnosticsSensor.Id, out SensorResult? networkResult) && networkResult.Success ? "ok" : "failed";

        securitySignals["capability_winget"] = state.Capabilities["winget"];
        securitySignals["capability_windows_updates"] = state.Capabilities["windows_update_sensor"];
    }

    private static void UpdatePerformanceSpikes(
        ServiceStateDto state,
        IReadOnlyDictionary<string, SensorResult> sensorResults,
        DateTimeOffset nowUtc)
    {
        if (!sensorResults.TryGetValue(Sensors.PerformanceWatchSensor.Id, out SensorResult? performanceResult) ||
            !performanceResult.Success ||
            performanceResult.Payload is not PerformanceWatchSensorData perf)
        {
            return;
        }

        if (perf.CpuPercent >= 90)
        {
            state.PerformanceSpikes.Add(new PerformanceSpikeRecordDto
            {
                TimestampUtc = nowUtc,
                Metric = "cpu",
                Value = perf.CpuPercent,
                ProcessName = perf.TopProcessName,
                ProcessId = perf.TopProcessId
            });
        }

        if (perf.MemoryPercent >= 90)
        {
            state.PerformanceSpikes.Add(new PerformanceSpikeRecordDto
            {
                TimestampUtc = nowUtc,
                Metric = "memory",
                Value = perf.MemoryPercent,
                ProcessName = perf.TopProcessName,
                ProcessId = perf.TopProcessId
            });
        }

        DateTimeOffset cutoff = nowUtc.AddDays(-30);
        state.PerformanceSpikes = state.PerformanceSpikes
            .Where(x => x.TimestampUtc >= cutoff)
            .OrderByDescending(x => x.TimestampUtc)
            .Take(3000)
            .ToList();
    }

    private static FindingDto? BuildPerformanceSpikeSummaryFinding(ServiceStateDto state, DateTimeOffset nowUtc)
    {
        if (state.PerformanceSpikes.Count == 0)
        {
            return null;
        }

        DateTimeOffset startOfDay = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, TimeSpan.Zero);
        int spikesToday = state.PerformanceSpikes.Count(x => x.TimestampUtc >= startOfDay);
        if (spikesToday <= 0)
        {
            return null;
        }

        PerformanceSpikeRecordDto latest = state.PerformanceSpikes
            .OrderByDescending(x => x.TimestampUtc)
            .First();

        return new FindingDto
        {
            FindingId = "health.performance.spikes_today",
            RuleId = "rule.performance.history",
            Category = FindingCategory.Health,
            Severity = spikesToday >= 3 ? FindingSeverity.Warning : FindingSeverity.Info,
            Title = "Performance Watch",
            Summary = $"Heute {spikesToday} Lastspitzen erkannt. Letzte Ursache: {latest.ProcessName} ({latest.Value:0}%).",
            DetectedAtUtc = nowUtc,
            Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["spikes_today"] = spikesToday.ToString(),
                ["latest_metric"] = latest.Metric,
                ["latest_value"] = latest.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                ["latest_process"] = latest.ProcessName,
                ["latest_timestamp_utc"] = latest.TimestampUtc.ToString("O")
            }
        };
    }

    private static void UpdateDailyHealthSnapshots(
        ServiceStateDto state,
        DateTimeOffset nowUtc,
        int healthScore,
        int openFindings,
        int resolvedFindings)
    {
        DateTime day = nowUtc.UtcDateTime.Date;
        HealthScoreDailySnapshotDto? existing = state.DailyHealthSnapshots.FirstOrDefault(x => x.DateUtc == day);
        if (existing is null)
        {
            state.DailyHealthSnapshots.Add(new HealthScoreDailySnapshotDto
            {
                DateUtc = day,
                HealthScore = healthScore,
                OpenFindings = openFindings,
                ResolvedFindings = resolvedFindings
            });
        }
        else
        {
            existing.HealthScore = healthScore;
            existing.OpenFindings = openFindings;
            existing.ResolvedFindings = Math.Max(existing.ResolvedFindings, resolvedFindings);
        }

        DateTime cutoff = nowUtc.UtcDateTime.Date.AddDays(-90);
        state.DailyHealthSnapshots = state.DailyHealthSnapshots
            .Where(x => x.DateUtc >= cutoff)
            .OrderBy(x => x.DateUtc)
            .ToList();
    }

    private static void EnrichMonthlyHealthSignals(
        ServiceStateDto state,
        Dictionary<string, string> signals,
        DateTimeOffset nowUtc,
        int openFindings,
        int resolvedFindingsCurrentScan)
    {
        DateTime cutoff = nowUtc.UtcDateTime.Date.AddDays(-30);
        List<HealthScoreDailySnapshotDto> monthly = state.DailyHealthSnapshots
            .Where(x => x.DateUtc >= cutoff)
            .OrderBy(x => x.DateUtc)
            .ToList();

        if (monthly.Count == 0)
        {
            signals["monthly_score_delta"] = "0";
            signals["monthly_resolved_count"] = "0";
            signals["monthly_open_count"] = openFindings.ToString();
            signals["monthly_summary"] = "Keine Monatsdaten verfuegbar.";
            return;
        }

        HealthScoreDailySnapshotDto first = monthly.First();
        HealthScoreDailySnapshotDto last = monthly.Last();
        int delta = last.HealthScore - first.HealthScore;
        int resolvedCount = monthly.Sum(x => x.ResolvedFindings);

        signals["monthly_score_delta"] = delta.ToString();
        signals["monthly_resolved_count"] = resolvedCount.ToString();
        signals["monthly_open_count"] = openFindings.ToString();
        signals["monthly_summary"] =
            $"Monat: Score {(delta >= 0 ? "+" : string.Empty)}{delta}, {resolvedCount} behoben, {openFindings} offen.";
        signals["monthly_trend_points"] = string.Join(",", monthly.Select(x => x.HealthScore.ToString()));
        signals["monthly_last_resolved"] = resolvedFindingsCurrentScan.ToString();
    }

    private Dictionary<string, bool> UpdateHistory(List<FindingDto> currentFindings, ServiceStateDto state, DateTimeOffset nowUtc)
    {
        var changedMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var currentLookup = currentFindings.ToDictionary(f => f.FindingId, StringComparer.OrdinalIgnoreCase);

        foreach (FindingDto finding in currentFindings)
        {
            if (!state.History.TryGetValue(finding.FindingId, out FindingHistoryRecordDto? record))
            {
                record = new FindingHistoryRecordDto();
                state.History[finding.FindingId] = record;
            }

            bool wasActive = record.Active;
            string evidenceHash = FindingFingerprint.ComputeEvidenceHash(finding);
            bool changed = !wasActive
                           || record.LastSeverity != finding.Severity
                           || !string.Equals(record.LastEvidenceHash, evidenceHash, StringComparison.Ordinal);

            if (!record.FirstSeenUtc.HasValue)
            {
                record.FirstSeenUtc = nowUtc;
                AddTimelineEvent(state, new TimelineEventDto
                {
                    Kind = "finding_detected",
                    Level = SeverityToLevel(finding.Severity),
                    Title = "Neues Problem erkannt",
                    Message = finding.Title,
                    FindingId = finding.FindingId
                });
            }
            else if (changed)
            {
                AddTimelineEvent(state, new TimelineEventDto
                {
                    Kind = "finding_changed",
                    Level = SeverityToLevel(finding.Severity),
                    Title = "Problem aktualisiert",
                    Message = finding.Title,
                    FindingId = finding.FindingId
                });
            }

            record.Active = true;
            record.LastSeenUtc = nowUtc;
            record.ResolvedAtUtc = null;
            record.LastSeverity = finding.Severity;
            record.LastEvidenceHash = evidenceHash;
            record.ActiveStreakScans = wasActive ? record.ActiveStreakScans + 1 : 1;

            if (changed)
            {
                record.LastChangedUtc = nowUtc;
            }
            else
            {
                record.LastChangedUtc ??= nowUtc;
            }

            changedMap[finding.FindingId] = changed;
        }

        var activeBefore = state.History
            .Where(kvp => kvp.Value.Active)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string findingId in activeBefore)
        {
            if (currentLookup.ContainsKey(findingId))
            {
                continue;
            }

            FindingHistoryRecordDto record = state.History[findingId];
            record.Active = false;
            record.ResolvedAtUtc = nowUtc;
            record.LastChangedUtc = nowUtc;
            record.ActiveStreakScans = 0;

            FindingDto snapshot = BuildResolvedSnapshot(findingId, record, nowUtc);
            snapshot.State.IsResolvedRecently = true;
            snapshot.State.ResolvedAtUtc = nowUtc;
            snapshot.State.FirstSeenUtc = record.FirstSeenUtc;
            snapshot.State.LastSeenUtc = record.LastSeenUtc;

            state.ResolvedRecentlyQueue.RemoveAll(x => x.Finding.FindingId.Equals(findingId, StringComparison.OrdinalIgnoreCase));
            state.ResolvedRecentlyQueue.Add(new ResolvedFindingCacheItemDto
            {
                Finding = snapshot,
                ResolvedAtUtc = nowUtc
            });

            AddTimelineEvent(state, new TimelineEventDto
            {
                Kind = "finding_resolved",
                Level = "success",
                Title = "Problem behoben",
                Message = snapshot.Title,
                FindingId = findingId
            });
        }

        int keepHours = Math.Max(_uxOptions.ResolvedRecentlyHours, 48);
        DateTimeOffset cutoff = nowUtc.AddHours(-keepHours);
        state.ResolvedRecentlyQueue = state.ResolvedRecentlyQueue
            .Where(x => x.ResolvedAtUtc >= cutoff)
            .OrderByDescending(x => x.ResolvedAtUtc)
            .Take(50)
            .ToList();

        return changedMap;
    }

    private FindingDto BuildResolvedSnapshot(string findingId, FindingHistoryRecordDto record, DateTimeOffset nowUtc)
    {
        FindingDto? previous = _lastReport.Findings
            .FirstOrDefault(f => f.FindingId.Equals(findingId, StringComparison.OrdinalIgnoreCase));

        FindingDto snapshot = previous is not null
            ? CloneFinding(previous)
            : new FindingDto
            {
                FindingId = findingId,
                RuleId = "history.resolved",
                Category = GuessCategoryFromFindingId(findingId),
                Severity = record.LastSeverity,
                Title = findingId,
                Summary = "Problem wurde kuerzlich behoben.",
                DetectedAtUtc = record.LastSeenUtc ?? nowUtc
            };

        snapshot.Priority = record.LastPriority;
        snapshot.DetectedAtUtc = record.LastSeenUtc ?? snapshot.DetectedAtUtc;
        snapshot.State.FirstSeenUtc = record.FirstSeenUtc;
        snapshot.State.LastSeenUtc = record.LastSeenUtc;
        snapshot.State.ResolvedAtUtc = record.ResolvedAtUtc;
        snapshot.State.IsResolvedRecently = true;
        snapshot.State.IsNew = false;
        snapshot.State.ActiveDays = record.FirstSeenUtc.HasValue
            ? Math.Max(1, (int)Math.Floor(((record.LastSeenUtc ?? nowUtc) - record.FirstSeenUtc.Value).TotalDays) + 1)
            : 0;
        snapshot.State.ActiveStreakScans = record.ActiveStreakScans;

        return snapshot;
    }

    private void ApplyLifecycleState(
        List<FindingDto> currentFindings,
        ServiceStateDto state,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, bool> changedMap)
    {
        foreach (FindingDto finding in currentFindings)
        {
            if (!state.History.TryGetValue(finding.FindingId, out FindingHistoryRecordDto? history))
            {
                continue;
            }

            state.Findings.TryGetValue(finding.FindingId, out FindingStateDto? suppression);

            DateTimeOffset firstSeen = history.FirstSeenUtc ?? nowUtc;
            int activeDays = Math.Max(1, (int)Math.Floor((nowUtc - firstSeen).TotalDays) + 1);
            bool isNewByTime = (nowUtc - firstSeen).TotalHours <= _uxOptions.NewFindingHours;
            bool isNewByChange = changedMap.TryGetValue(finding.FindingId, out bool changed) && changed && history.ActiveStreakScans <= 1;

            finding.State = new FindingStateDto
            {
                IsIgnored = suppression?.IsIgnored ?? false,
                SnoozedUntilUtc = suppression?.SnoozedUntilUtc,
                IsNew = isNewByTime || isNewByChange,
                IsResolvedRecently = false,
                ActiveDays = activeDays,
                ActiveStreakScans = history.ActiveStreakScans,
                FirstSeenUtc = history.FirstSeenUtc,
                LastSeenUtc = history.LastSeenUtc,
                ResolvedAtUtc = history.ResolvedAtUtc
            };
        }
    }

    private List<FindingDto> BuildRecentlyResolvedList(ServiceStateDto state, DateTimeOffset nowUtc)
    {
        DateTimeOffset cutoff = nowUtc.AddHours(-_uxOptions.ResolvedRecentlyHours);

        return state.ResolvedRecentlyQueue
            .Where(item => item.ResolvedAtUtc >= cutoff)
            .OrderByDescending(item => item.ResolvedAtUtc)
            .Take(5)
            .Select(item =>
            {
                FindingDto clone = CloneFinding(item.Finding);
                clone.State.IsResolvedRecently = true;
                clone.State.ResolvedAtUtc = item.ResolvedAtUtc;
                return clone;
            })
            .ToList();
    }

    private static bool IsSuppressed(ServiceStateDto state, string findingId, DateTimeOffset nowUtc)
    {
        if (!state.Findings.TryGetValue(findingId, out FindingStateDto? stateEntry))
        {
            return false;
        }

        if (stateEntry.IsIgnored)
        {
            return true;
        }

        if (stateEntry.SnoozedUntilUtc.HasValue && stateEntry.SnoozedUntilUtc.Value > nowUtc)
        {
            return true;
        }

        return false;
    }

    private async Task<MaintenanceStepResultDto> ExecuteMaintenanceRemediationStepAsync(
        string stepId,
        string title,
        string description,
        MaintenanceRiskLevel riskLevel,
        string actionId,
        string remediationId,
        CancellationToken cancellationToken)
    {
        var step = new MaintenanceStepResultDto
        {
            StepId = stepId,
            Title = title,
            Description = description,
            RiskLevel = riskLevel
        };

        if (!_remediations.TryGetValue(remediationId, out IRemediation? remediation))
        {
            step.Skipped = true;
            step.Success = false;
            step.Message = "Schritt ist auf diesem System aktuell nicht verfuegbar.";
            return step;
        }

        try
        {
            RemediationResult remediationResult = await remediation.ExecuteAsync(
                new RemediationRequest { SimulationMode = false },
                null,
                cancellationToken);

            step.Success = remediationResult.Success;
            step.Skipped = false;
            step.Message = string.IsNullOrWhiteSpace(remediationResult.Message)
                ? (remediationResult.Success ? "Erfolgreich ausgefuehrt." : "Ausfuehrung fehlgeschlagen.")
                : remediationResult.Message;

            return step;
        }
        catch (Exception ex)
        {
            step.Success = false;
            step.Skipped = false;
            step.Message = $"{actionId}: {ex.Message}";
            return step;
        }
    }

    private static async Task<MaintenanceStepResultDto> ExecuteWindowsUpdateMaintenanceStepAsync(CancellationToken cancellationToken)
    {
        var step = new MaintenanceStepResultDto
        {
            StepId = "maintenance.windows_updates.security",
            Title = "Windows Sicherheitsupdates anstossen",
            Description = "Startet den Windows-Update-Scan und Installationslauf fuer Qualitaetsupdates.",
            RiskLevel = MaintenanceRiskLevel.RestartPossible
        };

        try
        {
            ProcessExecutionResult scan = await ProcessRunner.RunAsync(
                "UsoClient.exe",
                "StartScan",
                TimeSpan.FromSeconds(30),
                cancellationToken);
            ProcessExecutionResult download = await ProcessRunner.RunAsync(
                "UsoClient.exe",
                "StartDownload",
                TimeSpan.FromSeconds(30),
                cancellationToken);
            ProcessExecutionResult install = await ProcessRunner.RunAsync(
                "UsoClient.exe",
                "StartInstall",
                TimeSpan.FromSeconds(30),
                cancellationToken);

            bool success = !scan.TimedOut && !download.TimedOut && !install.TimedOut;
            step.Success = success;
            step.Skipped = false;
            step.Message = success
                ? "Update-Scan/Download/Install wurde angestossen."
                : $"Update-Schritt nicht vollstaendig: Scan={scan.ExitCode}, Download={download.ExitCode}, Install={install.ExitCode}.";
        }
        catch (Exception ex)
        {
            step.Success = false;
            step.Skipped = false;
            step.Message = $"Windows-Update konnte nicht angestossen werden: {ex.Message}";
        }

        return step;
    }

    private static async Task<MaintenanceStepResultDto> ExecuteSafeCleanupStepAsync(CancellationToken cancellationToken)
    {
        var step = new MaintenanceStepResultDto
        {
            StepId = "maintenance.cleanup.safe",
            Title = "Sichere Bereinigung",
            Description = "Fuehrt StartComponentCleanup und Temp-Bereinigung durch (ohne Nutzerdokumente).",
            RiskLevel = MaintenanceRiskLevel.AdminRequired
        };

        try
        {
            ProcessExecutionResult dism = await ProcessRunner.RunAsync(
                "Dism.exe",
                "/Online /Cleanup-Image /StartComponentCleanup",
                TimeSpan.FromMinutes(8),
                cancellationToken);

            ProcessExecutionResult tempCleanup = await PowerShellRunner.RunAsync(
                "$ErrorActionPreference='SilentlyContinue'; " +
                "Get-ChildItem -Path $env:TEMP -Force -ErrorAction SilentlyContinue | " +
                "Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; " +
                "Write-Output 'TEMP_OK';",
                TimeSpan.FromMinutes(2),
                cancellationToken);

            bool success = !dism.TimedOut && dism.ExitCode == 0 && !tempCleanup.TimedOut;
            step.Success = success;
            step.Skipped = false;
            step.Message = success
                ? "Systemkomponenten und Temp-Dateien wurden bereinigt."
                : $"Bereinigung nicht vollstaendig: DISM={dism.ExitCode}, Temp={tempCleanup.ExitCode}.";
        }
        catch (Exception ex)
        {
            step.Success = false;
            step.Skipped = false;
            step.Message = $"Bereinigung fehlgeschlagen: {ex.Message}";
        }

        return step;
    }

    private static Dictionary<string, string> BuildSecuritySignals(IReadOnlyDictionary<string, SensorResult> sensorResults)
    {
        var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["smartscreen"] = "unknown",
            ["tamper_protection"] = "unknown",
            ["controlled_folder_access"] = "unknown",
            ["exploit_protection"] = "unknown",
            ["lsa_protection"] = "unknown",
            ["credential_guard"] = "unknown"
        };

        if (sensorResults.TryGetValue(SecurityHardeningSensor.Id, out SensorResult? hardeningResult) &&
            hardeningResult.Success &&
            hardeningResult.Payload is SecurityHardeningSensorData hardening)
        {
            signals["smartscreen"] = ToSignalStatus(hardening.SmartScreenEnabled, hardening.SmartScreenMode);
            signals["controlled_folder_access"] = ToSignalStatus(hardening.ControlledFolderAccessEnabled, hardening.ControlledFolderAccessMode);
            signals["exploit_protection"] = ToSignalStatus(hardening.ExploitProtectionEnabled);
            signals["lsa_protection"] = ToSignalStatus(hardening.LsaProtectionEnabled);
            signals["credential_guard"] = ToSignalStatus(hardening.CredentialGuardEnabled);
        }

        if (sensorResults.TryGetValue(DefenderSensor.Id, out SensorResult? defenderResult) &&
            defenderResult.Success &&
            defenderResult.Payload is DefenderSensorData defender)
        {
            signals["tamper_protection"] = ToSignalStatus(defender.TamperProtectionEnabled);
        }

        return signals;
    }

    private static string ToSignalStatus(bool? enabled, string? mode = null)
    {
        if (!string.IsNullOrWhiteSpace(mode))
        {
            string normalizedMode = mode.Trim();
            if (normalizedMode.Equals("Audit", StringComparison.OrdinalIgnoreCase))
            {
                return "audit";
            }

            if (normalizedMode.Equals("Warn", StringComparison.OrdinalIgnoreCase))
            {
                return "warn";
            }

            if (normalizedMode.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
                normalizedMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return "off";
            }

            if (normalizedMode.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
                normalizedMode.Equals("RequireAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return "on";
            }
        }

        return enabled switch
        {
            true => "on",
            false => "off",
            _ => "unknown"
        };
    }

    private static string ComputeOverallStatus(IReadOnlyCollection<FindingDto> findings)
    {
        if (findings.Any(f => f.Severity == FindingSeverity.Critical))
        {
            return "critical";
        }

        if (findings.Any(f => f.Severity == FindingSeverity.Warning))
        {
            return "warning";
        }

        if (findings.Any(f => f.Severity == FindingSeverity.Info))
        {
            return "info";
        }

        return "ok";
    }

    private static FindingCategory GuessCategoryFromFindingId(string findingId)
    {
        if (findingId.StartsWith("security.", StringComparison.OrdinalIgnoreCase))
        {
            return FindingCategory.Security;
        }

        if (findingId.StartsWith("storage.", StringComparison.OrdinalIgnoreCase))
        {
            return FindingCategory.Storage;
        }

        if (findingId.StartsWith("system.", StringComparison.OrdinalIgnoreCase))
        {
            return FindingCategory.System;
        }

        return FindingCategory.Health;
    }

    private static RuleThresholdsDto CloneThresholds(RuleThresholdsDto source)
    {
        var clone = new RuleThresholdsDto
        {
            StorageCriticalPercentFree = source.StorageCriticalPercentFree,
            StorageWarningPercentFree = source.StorageWarningPercentFree,
            EventLogWarningCount24h = source.EventLogWarningCount24h,
            DefenderSignatureWarningDays = source.DefenderSignatureWarningDays,
            DefenderSignatureCriticalDays = source.DefenderSignatureCriticalDays
        };
        clone.Normalize();
        return clone;
    }

    private static string SeverityToLevel(FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => "critical",
            FindingSeverity.Warning => "warning",
            _ => "info"
        };
    }

    private static void AddTimelineEvent(ServiceStateDto state, TimelineEventDto entry)
    {
        state.Timeline ??= new List<TimelineEventDto>();
        if (string.IsNullOrWhiteSpace(entry.EventId))
        {
            entry.EventId = Guid.NewGuid().ToString("N");
        }

        if (entry.TimestampUtc == default)
        {
            entry.TimestampUtc = DateTimeOffset.UtcNow;
        }

        state.Timeline.Insert(0, entry);
        if (state.Timeline.Count > 240)
        {
            state.Timeline = state.Timeline.Take(240).ToList();
        }
    }

    private static List<TimelineEventDto> BuildTimeline(ServiceStateDto state)
    {
        return (state.Timeline ?? new List<TimelineEventDto>())
            .OrderByDescending(x => x.TimestampUtc)
            .Take(80)
            .Select(x => new TimelineEventDto
            {
                EventId = x.EventId,
                TimestampUtc = x.TimestampUtc,
                Kind = x.Kind,
                Level = x.Level,
                Title = x.Title,
                Message = x.Message,
                FindingId = x.FindingId,
                ActionId = x.ActionId,
                ActionExecutionId = x.ActionExecutionId
            })
            .ToList();
    }

    private static List<AutoFixLogItemDto> BuildAutoFixLog(ServiceStateDto state)
    {
        return (state.ActionExecutions ?? new List<ActionExecutionRecordDto>())
            .Where(x => x.FinishedAtUtc.HasValue)
            .OrderByDescending(x => x.FinishedAtUtc)
            .Take(40)
            .Select(x => new AutoFixLogItemDto
            {
                TimestampUtc = x.FinishedAtUtc ?? x.StartedAtUtc,
                FindingId = x.FindingId ?? "unknown",
                ActionId = x.ActionId,
                ActionExecutionId = x.ActionExecutionId,
                RollbackAvailable = x.RollbackAvailable,
                RollbackHint = x.RollbackHint,
                Success = x.Success,
                Message = x.Message
            })
            .ToList();
    }

    private static BaselineDriftSummaryDto BuildBaselineDriftSummary(ServiceStateDto state, IReadOnlyCollection<FindingDto> findings)
    {
        if (state.Baseline is null || state.Baseline.Findings.Count == 0)
        {
            return new BaselineDriftSummaryDto
            {
                HasBaseline = false,
                BaselineCreatedAtUtc = null,
                BaselineLabel = string.Empty
            };
        }

        Dictionary<string, BaselineItemDto> baseline = state.Baseline.Findings
            .GroupBy(x => x.FindingId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, FindingDto> current = findings
            .Where(f => !f.FindingId.Equals("system.baseline.drift", StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => f.FindingId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        int newFindings = current.Keys.Except(baseline.Keys, StringComparer.OrdinalIgnoreCase).Count();
        int resolvedFindings = baseline.Keys.Except(current.Keys, StringComparer.OrdinalIgnoreCase).Count();

        int changedFindings = 0;
        foreach ((string findingId, FindingDto finding) in current)
        {
            if (!baseline.TryGetValue(findingId, out BaselineItemDto? baseItem))
            {
                continue;
            }

            string currentHash = FindingFingerprint.ComputeEvidenceHash(finding);
            bool changed = baseItem.Severity != finding.Severity
                           || !string.Equals(baseItem.EvidenceHash, currentHash, StringComparison.Ordinal);
            if (changed)
            {
                changedFindings++;
            }
        }

        return new BaselineDriftSummaryDto
        {
            HasBaseline = true,
            BaselineCreatedAtUtc = state.Baseline.CreatedAtUtc,
            BaselineLabel = state.Baseline.Label,
            NewFindings = newFindings,
            ChangedFindings = changedFindings,
            ResolvedFindings = resolvedFindings
        };
    }

    private static FindingDto? BuildBaselineDriftFinding(BaselineDriftSummaryDto summary, DateTimeOffset nowUtc)
    {
        if (!summary.HasBaseline)
        {
            return null;
        }

        int totalDrift = summary.NewFindings + summary.ChangedFindings + summary.ResolvedFindings;
        if (totalDrift == 0)
        {
            return null;
        }

        FindingSeverity severity = (summary.NewFindings > 0 || summary.ChangedFindings > 0)
            ? FindingSeverity.Warning
            : FindingSeverity.Info;

        return new FindingDto
        {
            FindingId = "system.baseline.drift",
            RuleId = "rule.baseline.drift",
            Category = FindingCategory.System,
            Severity = severity,
            Title = "Abweichung zur Baseline erkannt",
            Summary = $"Neu: {summary.NewFindings}, geaendert: {summary.ChangedFindings}, behoben: {summary.ResolvedFindings}.",
            DetailsMarkdown =
                $"Vergleich mit Baseline '{summary.BaselineLabel}' vom {summary.BaselineCreatedAtUtc?.ToLocalTime():g}.\n\n" +
                "Pruefen Sie neue oder geaenderte Punkte, um unerwartete Systemaenderungen schnell zu erkennen.",
            DetectedAtUtc = nowUtc,
            Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseline_label"] = summary.BaselineLabel,
                ["baseline_created_utc"] = summary.BaselineCreatedAtUtc?.ToString("O") ?? "unknown",
                ["new_findings"] = summary.NewFindings.ToString(),
                ["changed_findings"] = summary.ChangedFindings.ToString(),
                ["resolved_findings"] = summary.ResolvedFindings.ToString()
            }
        };
    }

    private static void EnrichCriticalityDetails(IEnumerable<FindingDto> findings)
    {
        foreach (FindingDto finding in findings)
        {
            string why = finding.Severity switch
            {
                FindingSeverity.Critical => "Warum kritisch? Dieses Problem kann Schutz oder Stabilitaet direkt beeintraechtigen und sollte zeitnah behoben werden.",
                FindingSeverity.Warning => "Warum wichtig? Dieses Problem ist noch nicht akut kritisch, kann aber Sicherheit oder Performance verschlechtern.",
                _ => "Einordnung: Dies ist ein Hinweis zur Optimierung und Beobachtung."
            };

            if (string.IsNullOrWhiteSpace(finding.DetailsMarkdown))
            {
                finding.DetailsMarkdown = why;
                continue;
            }

            if (finding.DetailsMarkdown.Contains("Warum kritisch?", StringComparison.OrdinalIgnoreCase)
                || finding.DetailsMarkdown.Contains("Warum wichtig?", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            finding.DetailsMarkdown = finding.DetailsMarkdown.TrimEnd() + Environment.NewLine + Environment.NewLine + why;
        }
    }

    private static void EnrichFindingGuidance(IEnumerable<FindingDto> findings)
    {
        foreach (FindingDto finding in findings)
        {
            if (string.IsNullOrWhiteSpace(finding.WhatIsThis))
            {
                finding.WhatIsThis = BuildWhatIsThisText(finding);
            }

            if (string.IsNullOrWhiteSpace(finding.WhyImportant))
            {
                finding.WhyImportant = BuildWhyImportantText(finding);
            }

            if (string.IsNullOrWhiteSpace(finding.RecommendedAction))
            {
                finding.RecommendedAction = BuildRecommendedActionText(finding);
            }

            if (string.IsNullOrWhiteSpace(finding.RiskEffort))
            {
                finding.RiskEffort = BuildRiskEffortText(finding);
            }
        }
    }

    private static string BuildWhatIsThisText(FindingDto finding)
    {
        return finding.FindingId.ToLowerInvariant() switch
        {
            "security.defender.realtime_off" =>
                "Der Windows Defender Echtzeitschutz ist derzeit deaktiviert.",
            "security.defender.signatures_old" =>
                "Die lokalen Defender-Signaturen sind nicht mehr aktuell.",
            "security.firewall.disabled" =>
                "Mindestens ein Windows-Firewall-Profil ist deaktiviert.",
            "security.bitlocker.off" =>
                "Das Systemlaufwerk ist aktuell nicht mit BitLocker geschuetzt.",
            "storage.system.low_space" =>
                "Auf dem Systemlaufwerk steht zu wenig freier Speicher zur Verfuegung.",
            "system.reboot.pending" =>
                "Windows signalisiert, dass ein Neustart fuer ausstehende Aenderungen noetig ist.",
            "health.eventlog.system_errors" =>
                "Im System-Eventlog wurden in den letzten 24 Stunden viele kritische Fehler gefunden.",
            "health.eventlog.app_errors" =>
                "Im Application-Eventlog wurden in den letzten 24 Stunden viele kritische Fehler gefunden.",
            "system.baseline.drift" =>
                "Die aktuelle Systemlage weicht von Ihrer gespeicherten Baseline ab.",
            _ => !string.IsNullOrWhiteSpace(finding.Summary)
                ? finding.Summary
                : "Dieser Eintrag beschreibt eine erkannte Abweichung vom Sollzustand."
        };
    }

    private static string BuildWhyImportantText(FindingDto finding)
    {
        return finding.FindingId.ToLowerInvariant() switch
        {
            "security.defender.realtime_off" =>
                "Ohne Echtzeitschutz koennen neue Bedrohungen nicht sofort blockiert werden.",
            "security.defender.signatures_old" =>
                "Veraltete Signaturen reduzieren die Erkennungsrate bei aktuellen Bedrohungen.",
            "security.firewall.disabled" =>
                "Deaktivierte Firewall-Profile erhoehen das Risiko unerwuenschter Netzwerkzugriffe.",
            "security.bitlocker.off" =>
                "Ohne Laufwerksverschluesselung sind Daten bei Geraeteverlust leichter auslesbar.",
            "storage.system.low_space" =>
                "Wenig Speicher kann Updates, Stabilitaet und Performance deutlich verschlechtern.",
            "system.reboot.pending" =>
                "Ausstehende Neustarts lassen Sicherheits- oder Treiberupdates unvollstaendig.",
            "system.baseline.drift" =>
                "Abweichungen zur Baseline helfen, unerwartete Systemaenderungen frueh zu erkennen.",
            _ => finding.Severity switch
            {
                FindingSeverity.Critical => "Das Problem kann Schutz oder Stabilitaet direkt beeintraechtigen.",
                FindingSeverity.Warning => "Das Problem sollte zeitnah behoben werden, um Folgeschaeden zu vermeiden.",
                _ => "Der Hinweis verbessert Transparenz und Systemqualitaet."
            }
        };
    }

    private static string BuildRecommendedActionText(FindingDto finding)
    {
        if (finding.Actions.Count == 0)
        {
            return "Eintrag pruefen und bei Bedarf in den Detailansichten manuell nachziehen.";
        }

        ActionDto? serviceAction = finding.Actions
            .FirstOrDefault(a => a.Kind == ActionKind.RunRemediation);
        if (serviceAction is not null)
        {
            return $"Empfohlen: '{serviceAction.Label}' ausfuehren.";
        }

        ActionDto? firstAction = finding.Actions.FirstOrDefault();
        return firstAction is null
            ? "Eintrag pruefen und manuell beheben."
            : $"Empfohlen: '{firstAction.Label}' nutzen.";
    }

    private static string BuildRiskEffortText(FindingDto finding)
    {
        bool requiresAdmin = finding.Actions.Any(a => a.RequiresAdmin);
        bool restartPossible = finding.Actions.Any(a => a.MayRequireRestart)
                               || finding.FindingId.Equals("system.reboot.pending", StringComparison.OrdinalIgnoreCase);
        bool safeForOneClick = finding.Actions.Any(a => a.IsSafeForOneClickMaintenance);

        if (requiresAdmin && restartPossible)
        {
            return safeForOneClick
                ? "Sicher, Admin noetig, Neustart moeglich."
                : "Admin noetig, Neustart moeglich.";
        }

        if (requiresAdmin)
        {
            return safeForOneClick ? "Sicher, Admin noetig." : "Admin noetig.";
        }

        if (restartPossible)
        {
            return safeForOneClick ? "Sicher, Neustart moeglich." : "Neustart moeglich.";
        }

        return safeForOneClick ? "Sicher, geringes Risiko." : "Manueller Eingriff moeglich.";
    }

    private static (string? RollbackPowerShellCommand, string? RollbackHint) BuildRollbackPlan(ActionDto action, FindingDto finding)
    {
        if (action.ActionId.Equals(ActionIds.DefenderEnableRealtime, StringComparison.OrdinalIgnoreCase)
            && TryGetEvidenceBoolean(finding.Evidence, "realtime_enabled", out bool realtimeEnabled)
            && !realtimeEnabled)
        {
            return (
                "Set-MpPreference -DisableRealtimeMonitoring $true",
                "Rollback setzt den Defender-Echtzeitschutz wieder auf den vorherigen Zustand (deaktiviert).");
        }

        if (action.ActionId.Equals(ActionIds.FirewallEnableAll, StringComparison.OrdinalIgnoreCase))
        {
            var profiles = new List<string>();
            if (TryGetEvidenceBoolean(finding.Evidence, "profile_domain", out bool domainEnabled) && !domainEnabled)
            {
                profiles.Add("Domain");
            }

            if (TryGetEvidenceBoolean(finding.Evidence, "profile_private", out bool privateEnabled) && !privateEnabled)
            {
                profiles.Add("Private");
            }

            if (TryGetEvidenceBoolean(finding.Evidence, "profile_public", out bool publicEnabled) && !publicEnabled)
            {
                profiles.Add("Public");
            }

            if (profiles.Count > 0)
            {
                string profileList = string.Join(",", profiles);
                return (
                    $"Set-NetFirewallProfile -Profile {profileList} -Enabled False",
                    $"Rollback deaktiviert die vorher deaktivierten Firewall-Profile erneut ({string.Join(", ", profiles)}).");
            }
        }

        return (null, null);
    }

    private static bool TryGetEvidenceBoolean(IReadOnlyDictionary<string, string> evidence, string key, out bool value)
    {
        value = false;
        if (!evidence.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return bool.TryParse(raw, out value);
    }

    private static async Task<RestorePointResult> TryCreateRestorePointAsync(string actionId, CancellationToken cancellationToken)
    {
        string description = BuildRestorePointDescription(actionId);
        string command = $"Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS'";
        ProcessExecutionResult result = await PowerShellRunner.RunAsync(command, TimeSpan.FromMinutes(2), cancellationToken);

        if (result.TimedOut)
        {
            return new RestorePointResult(true, false, description, "Restore-Point konnte wegen Timeout nicht erstellt werden.");
        }

        if (result.ExitCode == 0)
        {
            return new RestorePointResult(true, true, description, $"Restore-Point '{description}' wurde erstellt.");
        }

        string message = BuildProcessErrorMessage("Restore-Point konnte nicht erstellt werden.", result);
        return new RestorePointResult(true, false, description, message);
    }

    private static string BuildRestorePointDescription(string actionId)
    {
        string compactActionId = actionId
            .Replace('.', '_')
            .Replace('-', '_');
        if (compactActionId.Length > 28)
        {
            compactActionId = compactActionId[..28];
        }

        return $"PCW_{compactActionId}_{DateTime.Now:yyyyMMdd_HHmm}";
    }

    private static string BuildProcessErrorMessage(string fallback, ProcessExecutionResult result)
    {
        if (result.TimedOut)
        {
            return fallback + " (Timeout)";
        }

        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            return result.StdErr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.StdOut))
        {
            return result.StdOut.Trim();
        }

        return fallback;
    }

    private readonly record struct RestorePointResult(bool Attempted, bool Created, string Description, string Message);

    private static FindingDto CloneFinding(FindingDto source)
    {
        var clone = new FindingDto
        {
            FindingId = source.FindingId,
            RuleId = source.RuleId,
            Category = source.Category,
            Severity = source.Severity,
            Priority = source.Priority,
            Title = source.Title,
            Summary = source.Summary,
            DetailsMarkdown = source.DetailsMarkdown,
            WhatIsThis = source.WhatIsThis,
            WhyImportant = source.WhyImportant,
            RecommendedAction = source.RecommendedAction,
            RiskEffort = source.RiskEffort,
            DetectedAtUtc = source.DetectedAtUtc,
            State = new FindingStateDto
            {
                IsIgnored = source.State.IsIgnored,
                SnoozedUntilUtc = source.State.SnoozedUntilUtc,
                IsNew = source.State.IsNew,
                IsResolvedRecently = source.State.IsResolvedRecently,
                ActiveDays = source.State.ActiveDays,
                ActiveStreakScans = source.State.ActiveStreakScans,
                FirstSeenUtc = source.State.FirstSeenUtc,
                LastSeenUtc = source.State.LastSeenUtc,
                ResolvedAtUtc = source.State.ResolvedAtUtc
            },
            Evidence = new Dictionary<string, string>(source.Evidence, StringComparer.OrdinalIgnoreCase)
        };

        clone.Actions = source.Actions
            .Select(a => new ActionDto
            {
                ActionId = a.ActionId,
                Label = a.Label,
                Kind = a.Kind,
                RemediationId = a.RemediationId,
                ExternalTarget = a.ExternalTarget,
                DetailsMarkdown = a.DetailsMarkdown,
                ConfirmText = a.ConfirmText,
                IsSafeForOneClickMaintenance = a.IsSafeForOneClickMaintenance,
                RequiresAdmin = a.RequiresAdmin,
                MayRequireRestart = a.MayRequireRestart
            })
            .ToList();

        return clone;
    }

    private static async Task WriteAtomicJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        string tempPath = path + ".tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private static void ApplyContextualRecommendations(List<FindingDto> findings, DeviceContextDto context)
    {
        foreach (FindingDto finding in findings)
        {
            if (context.IsLaptop && finding.FindingId.Equals("storage.system.low_space", StringComparison.OrdinalIgnoreCase))
            {
                finding.Summary = finding.Summary + " Auf Laptops kann wenig Speicher auch Updates und Standby stoeren.";
                finding.DetailsMarkdown = (finding.DetailsMarkdown ?? string.Empty)
                    + "\n\nHinweis Laptop: Freier Speicher beeinflusst oft Ruhezustand und Update-Installationen.";
            }

            if (context.IsDesktop && finding.FindingId.Equals("system.reboot.pending", StringComparison.OrdinalIgnoreCase))
            {
                finding.Summary = finding.Summary + " Auf Desktop-Systemen hilft ein Neustart oft beim Abschluss von Treiber- und Systemupdates.";
            }
        }
    }

    private static void InjectDebugFindings(List<FindingDto> findings, RuleContext context)
    {
        string raw = Environment.GetEnvironmentVariable("PCWACHTER_FAKE_FINDINGS") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var tokens = raw.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (tokens.Contains("CRITICAL_SECURITY"))
        {
            findings.Add(new FindingDto
            {
                FindingId = "debug.fake.critical_security",
                RuleId = "rule.debug.fake",
                Category = FindingCategory.Security,
                Severity = FindingSeverity.Critical,
                Title = "Debug: Kritisches Security-Problem",
                Summary = "Injectiertes Debug-Finding.",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "PCWACHTER_FAKE_FINDINGS"
                }
            });
        }

        if (tokens.Contains("WARNING_STORAGE"))
        {
            findings.Add(new FindingDto
            {
                FindingId = "debug.fake.warning_storage",
                RuleId = "rule.debug.fake",
                Category = FindingCategory.Storage,
                Severity = FindingSeverity.Warning,
                Title = "Debug: Speicherwarnung",
                Summary = "Injectierte Speicherwarnung.",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "PCWACHTER_FAKE_FINDINGS"
                }
            });
        }
    }
}
