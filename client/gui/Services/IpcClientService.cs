using System.Text.Json;
using PCWachter.Contracts;

namespace PCWachter.Desktop.Services;

public sealed partial class IpcClientService : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string[] _pipeCandidates = ["PCWaechter.Service.Ipc", "PCWachterPipe"];
    private readonly ReportStore _reportStore;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private CancellationTokenSource? _subscriptionCts;
    private Task? _subscriptionTask;
    private string _lastReportIdentity = string.Empty;

    public IpcClientService(ReportStore reportStore)
    {
        _reportStore = reportStore;
    }

    public bool MockMode { get; set; }
    public bool IsConnected { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<ActionProgressDto>? RemediationProgress;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            EnsureMockReport(refresh: false);
            SetConnection(true, string.Empty);
            return true;
        }

        ScanReportDto? report = await GetLatestReportAsync(cancellationToken, TimeSpan.FromSeconds(2));
        bool connected = report is not null;
        SetConnection(connected, connected ? string.Empty : LastError);
        return connected;
    }

    public async Task<ScanReportDto?> GetLatestReportAsync(
        CancellationToken cancellationToken = default,
        TimeSpan? timeoutOverride = null)
    {
        if (MockMode)
        {
            ScanReportDto report = EnsureMockReport(refresh: false);
            SetConnection(true, string.Empty);
            return report;
        }

        try
        {
            IpcResponseDto response = await SendRequestAsync(new IpcRequestDto
            {
                Type = IpcMessageTypes.GetReport,
                RequestId = Guid.NewGuid().ToString("N")
            }, cancellationToken, timeoutOverride);

            if (!response.Ok)
            {
                SetConnection(false, response.Error ?? "IPC request failed");
                return null;
            }

            if (string.IsNullOrWhiteSpace(response.PayloadJson))
            {
                SetConnection(false, "No report payload");
                return null;
            }

            ScanReportDto? report = JsonSerializer.Deserialize<ScanReportDto>(response.PayloadJson, JsonOptions);
            if (report is null)
            {
                SetConnection(false, "Could not parse report payload");
                return null;
            }

            SetConnection(true, string.Empty);
            UpdateReport(report);
            return report;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller canceled deliberately (retry/shutdown). Do not force disconnect state.
            return null;
        }
        catch (OperationCanceledException)
        {
            SetConnection(false, "Zeitüberschreitung bei Serviceanfrage.");
            return null;
        }
        catch (Exception ex)
        {
            SetConnection(false, ex.Message);
            return null;
        }
    }

    public async Task TriggerScanAsync(CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            EnsureMockReport(refresh: true);
            SetConnection(true, string.Empty);
            await Task.CompletedTask;
            return;
        }

        try
        {
            IpcResponseDto response = await SendRequestAsync(new IpcRequestDto
            {
                Type = IpcMessageTypes.TriggerScan,
                RequestId = Guid.NewGuid().ToString("N")
            }, cancellationToken, TimeSpan.FromSeconds(120));

            if (response.Ok && !string.IsNullOrWhiteSpace(response.PayloadJson))
            {
                ScanReportDto? report = JsonSerializer.Deserialize<ScanReportDto>(response.PayloadJson, JsonOptions);
                if (report is not null)
                {
                    SetConnection(true, string.Empty);
                    UpdateReport(report);
                    return;
                }
            }

            await GetLatestReportAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException)
        {
            SetConnection(false, "Zeitüberschreitung beim Starten des Scans.");
        }
        catch
        {
            await GetLatestReportAsync(cancellationToken);
        }
    }

    public async Task<ActionExecutionResultDto> RunActionAsync(
        string actionId,
        string? findingId = null,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            RemediationProgress?.Invoke(this, new ActionProgressDto
            {
                ActionId = actionId,
                Percent = 10,
                Message = "Mockup: Aktion wird simuliert..."
            });

            SimulateMockAction(actionId, findingId);

            RemediationProgress?.Invoke(this, new ActionProgressDto
            {
                ActionId = actionId,
                Percent = 100,
                Message = "Mockup: Aktion erfolgreich simuliert."
            });

            return await Task.FromResult(new ActionExecutionResultDto
            {
                ActionExecutionId = Guid.NewGuid().ToString("N"),
                Success = true,
                ExitCode = 0,
                Message = "Mockup-Modus: Aktion wurde nur visuell simuliert.",
                RollbackAvailable = false
            });
        }

        RemediationProgress?.Invoke(this, new ActionProgressDto
        {
            ActionId = actionId,
            Percent = 10,
            Message = "Aktion wird gestartet..."
        });

        var payload = new ExecuteActionRequestDto
        {
            ActionId = actionId,
            SimulationMode = false,
            Parameters = parameters
        };

        IpcResponseDto response = await SendRequestAsync(new IpcRequestDto
        {
            Type = IpcMessageTypes.ExecuteAction,
            RequestId = Guid.NewGuid().ToString("N"),
            PayloadJson = JsonSerializer.Serialize(payload)
        }, cancellationToken);

        if (!response.Ok)
        {
            ActionExecutionResultDto? failedResult = null;
            if (!string.IsNullOrWhiteSpace(response.PayloadJson))
            {
                failedResult = JsonSerializer.Deserialize<ActionExecutionResultDto>(response.PayloadJson, JsonOptions);
            }

            RemediationProgress?.Invoke(this, new ActionProgressDto
            {
                ActionId = actionId,
                Percent = 100,
                Message = failedResult?.Message ?? response.Error ?? "Aktion fehlgeschlagen"
            });

            return failedResult ?? new ActionExecutionResultDto
            {
                Success = false,
                ExitCode = 10,
                Message = response.Error ?? "Action failed"
            };
        }

        if (string.IsNullOrWhiteSpace(response.PayloadJson))
        {
            RemediationProgress?.Invoke(this, new ActionProgressDto
            {
                ActionId = actionId,
                Percent = 100,
                Message = "Aktion abgeschlossen"
            });

            return new ActionExecutionResultDto
            {
                Success = true,
                ExitCode = 0,
                Message = "Action triggered"
            };
        }

        ActionExecutionResultDto? result = JsonSerializer.Deserialize<ActionExecutionResultDto>(response.PayloadJson, JsonOptions);
        RemediationProgress?.Invoke(this, new ActionProgressDto
        {
            ActionId = actionId,
            Percent = 100,
            Message = result?.Message ?? "Aktion abgeschlossen"
        });

        return result ?? new ActionExecutionResultDto { Success = false, ExitCode = 10, Message = "Invalid action response" };
    }

    public async Task<MaintenanceRunResultDto> RunSafeMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            SimulateMockAction("action.maintenance.safe", "maintenance.safe");
            return await Task.FromResult(new MaintenanceRunResultDto
            {
                StartedAtUtc = now.AddSeconds(-4),
                FinishedAtUtc = now,
                RestartRecommended = false,
                Summary = "Mockup-Modus: Wartung wurde visuell simuliert.",
                Steps =
                [
                    new MaintenanceStepResultDto
                    {
                        StepId = "mock.defender.signatures",
                        Title = "Defender Signaturen",
                        Description = "Signaturen aktualisieren",
                        Success = true,
                        Message = "Simuliert"
                    },
                    new MaintenanceStepResultDto
                    {
                        StepId = "mock.storage.cleanup",
                        Title = "Temporäre Dateien",
                        Description = "Sichere Bereinigung",
                        Success = true,
                        Message = "Simuliert"
                    }
                ]
            });
        }

        IpcResponseDto response = await SendRequestAsync(new IpcRequestDto
        {
            Type = IpcMessageTypes.RunSafeMaintenance,
            RequestId = Guid.NewGuid().ToString("N")
        }, cancellationToken, TimeSpan.FromMinutes(5));

        if (string.IsNullOrWhiteSpace(response.PayloadJson))
        {
            return new MaintenanceRunResultDto
            {
                StartedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Summary = response.Error ?? "Keine Wartungsantwort vom Service."
            };
        }

        MaintenanceRunResultDto? result = JsonSerializer.Deserialize<MaintenanceRunResultDto>(response.PayloadJson, JsonOptions);
        if (result is null)
        {
            return new MaintenanceRunResultDto
            {
                StartedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Summary = "Wartungsantwort konnte nicht gelesen werden."
            };
        }

        if (!response.Ok && !string.IsNullOrWhiteSpace(response.Error))
        {
            result.Summary = response.Error;
        }

        return result;
    }

    public async Task SnoozeFindingAsync(string findingId, int days = 7, CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            SimulateMockAction("action.mock.snooze", findingId);
            await Task.CompletedTask;
            return;
        }

        var payload = new SetFindingStateRequestDto
        {
            FindingId = findingId,
            Ignore = false,
            SnoozeUntilUtc = DateTimeOffset.UtcNow.AddDays(days)
        };

        await SendRequestAsync(new IpcRequestDto
        {
            Type = IpcMessageTypes.SetFindingState,
            RequestId = Guid.NewGuid().ToString("N"),
            PayloadJson = JsonSerializer.Serialize(payload)
        }, cancellationToken);

        await TriggerScanAsync(cancellationToken);
    }

    public async Task IgnoreFindingAsync(string findingId, bool ignore = true, CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            SimulateMockAction("action.mock.ignore", findingId);
            await Task.CompletedTask;
            return;
        }

        var payload = new SetFindingStateRequestDto
        {
            FindingId = findingId,
            Ignore = ignore,
            SnoozeUntilUtc = null
        };

        await SendRequestAsync(new IpcRequestDto
        {
            Type = IpcMessageTypes.SetFindingState,
            RequestId = Guid.NewGuid().ToString("N"),
            PayloadJson = JsonSerializer.Serialize(payload)
        }, cancellationToken);

        await TriggerScanAsync(cancellationToken);
    }

    public Task<AutoFixPolicyDto?> GetAutoFixPolicyAsync(CancellationToken cancellationToken = default)
    {
        AutoFixPolicyDto policy = _reportStore.CurrentReport.AutoFixPolicy ?? new AutoFixPolicyDto();
        return Task.FromResult<AutoFixPolicyDto?>(policy);
    }

    public async Task<bool> SetAutoFixPolicyAsync(AutoFixPolicyDto policy, CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            ScanReportDto local = ReportStore.Clone(_reportStore.CurrentReport);
            local.AutoFixPolicy = policy;
            local.GeneratedAtUtc = DateTimeOffset.UtcNow;
            _reportStore.Update(local);
            return await Task.FromResult(true);
        }

        // API operation is optional. We keep UI responsive with a local fallback.
        try
        {
            await SendRequestAsync(new IpcRequestDto
            {
                Type = "set_autofix_policy",
                RequestId = Guid.NewGuid().ToString("N"),
                PayloadJson = JsonSerializer.Serialize(policy)
            }, cancellationToken);
        }
        catch
        {
            // Ignore unsupported command and continue with local report update.
        }

        ScanReportDto report = ReportStore.Clone(_reportStore.CurrentReport);
        report.AutoFixPolicy = policy;
        _reportStore.Update(report);
        return true;
    }

    public async Task<FeatureConfigDto?> GetFeatureConfigAsync(CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            ScanReportDto report = EnsureMockReport(refresh: false);
            return await Task.FromResult<FeatureConfigDto?>(new FeatureConfigDto
            {
                RuleThresholds = report.RuleThresholds ?? new RuleThresholdsDto(),
                Baseline = report.BaselineDrift ?? new BaselineDriftSummaryDto()
            });
        }

        try
        {
            IpcResponseDto response = await SendRequestAsync(new IpcRequestDto
            {
                Type = IpcMessageTypes.GetFeatureConfig,
                RequestId = Guid.NewGuid().ToString("N")
            }, cancellationToken);

            if (!response.Ok || string.IsNullOrWhiteSpace(response.PayloadJson))
            {
                return null;
            }

            return JsonSerializer.Deserialize<FeatureConfigDto>(response.PayloadJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> SetRuleThresholdsAsync(RuleThresholdsDto thresholds, CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            thresholds.Normalize();
            ScanReportDto local = ReportStore.Clone(_reportStore.CurrentReport);
            local.RuleThresholds = thresholds;
            local.GeneratedAtUtc = DateTimeOffset.UtcNow;
            _reportStore.Update(local);
            return await Task.FromResult(true);
        }

        thresholds.Normalize();

        var payload = new SetFeatureConfigRequestDto
        {
            RuleThresholds = thresholds
        };

        try
        {
            IpcResponseDto response = await SendRequestAsync(new IpcRequestDto
            {
                Type = IpcMessageTypes.SetFeatureConfig,
                RequestId = Guid.NewGuid().ToString("N"),
                PayloadJson = JsonSerializer.Serialize(payload)
            }, cancellationToken);

            if (!response.Ok)
            {
                return false;
            }

            ScanReportDto report = ReportStore.Clone(_reportStore.CurrentReport);
            report.RuleThresholds = thresholds;
            _reportStore.Update(report);
            return true;
        }
        catch
        {
            ScanReportDto report = ReportStore.Clone(_reportStore.CurrentReport);
            report.RuleThresholds = thresholds;
            _reportStore.Update(report);
            return true;
        }
    }

    public async Task<CreateBaselineResultDto> CreateBaselineAsync(string? label = null, CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            ScanReportDto report = EnsureMockReport(refresh: false);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            BaselineDriftSummaryDto baseline = new()
            {
                HasBaseline = true,
                BaselineLabel = string.IsNullOrWhiteSpace(label) ? "Mockup-Baseline" : label!.Trim(),
                BaselineCreatedAtUtc = now,
                NewFindings = 0,
                ChangedFindings = 0,
                ResolvedFindings = report.RecentlyResolved.Count
            };

            report.BaselineDrift = baseline;
            report.GeneratedAtUtc = now;
            _reportStore.Update(report);

            return await Task.FromResult(new CreateBaselineResultDto
            {
                Success = true,
                Message = "Mockup-Modus: Baseline wurde simuliert erstellt.",
                Baseline = baseline
            });
        }

        var payload = new CreateBaselineRequestDto
        {
            Label = label
        };

        try
        {
            IpcResponseDto response = await SendRequestAsync(new IpcRequestDto
            {
                Type = IpcMessageTypes.CreateBaseline,
                RequestId = Guid.NewGuid().ToString("N"),
                PayloadJson = JsonSerializer.Serialize(payload)
            }, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.PayloadJson))
            {
                return new CreateBaselineResultDto
                {
                    Success = response.Ok,
                    Message = response.Error ?? "Keine Antwort vom Service."
                };
            }

            CreateBaselineResultDto? result = JsonSerializer.Deserialize<CreateBaselineResultDto>(response.PayloadJson, JsonOptions);
            if (result is null)
            {
                return new CreateBaselineResultDto
                {
                    Success = false,
                    Message = "Baseline-Antwort konnte nicht gelesen werden."
                };
            }

            if (!response.Ok)
            {
                result.Success = false;
                if (!string.IsNullOrWhiteSpace(response.Error))
                {
                    result.Message = response.Error;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new CreateBaselineResultDto
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<ActionExecutionResultDto> RollbackActionAsync(string actionExecutionId, CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            ScanReportDto report = ReportStore.Clone(_reportStore.CurrentReport);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            report.Timeline.Insert(0, new TimelineEventDto
            {
                TimestampUtc = now,
                Kind = "rollback_succeeded",
                Level = "success",
                Title = "Rollback simuliert",
                Message = $"Mockup: Rollback für {actionExecutionId} wurde simuliert.",
                ActionExecutionId = actionExecutionId
            });
            if (report.Timeline.Count > 80)
            {
                report.Timeline = report.Timeline.Take(80).ToList();
            }

            report.GeneratedAtUtc = now;
            _reportStore.Update(report);

            return await Task.FromResult(new ActionExecutionResultDto
            {
                ActionExecutionId = actionExecutionId,
                Success = true,
                ExitCode = 0,
                Message = "Mockup-Modus: Rollback wurde visuell simuliert."
            });
        }

        var payload = new RollbackActionRequestDto
        {
            ActionExecutionId = actionExecutionId
        };

        try
        {
            IpcResponseDto response = await SendRequestAsync(new IpcRequestDto
            {
                Type = IpcMessageTypes.RollbackAction,
                RequestId = Guid.NewGuid().ToString("N"),
                PayloadJson = JsonSerializer.Serialize(payload)
            }, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.PayloadJson))
            {
                return new ActionExecutionResultDto
                {
                    ActionExecutionId = actionExecutionId,
                    Success = response.Ok,
                    ExitCode = response.Ok ? 0 : 10,
                    Message = response.Error ?? (response.Ok ? "Rollback ausgeführt." : "Rollback fehlgeschlagen.")
                };
            }

            ActionExecutionResultDto? result = JsonSerializer.Deserialize<ActionExecutionResultDto>(response.PayloadJson, JsonOptions);
            if (result is null)
            {
                return new ActionExecutionResultDto
                {
                    ActionExecutionId = actionExecutionId,
                    Success = false,
                    ExitCode = 10,
                    Message = "Rollback-Antwort konnte nicht gelesen werden."
                };
            }

            if (!response.Ok)
            {
                result.Success = false;
                if (!string.IsNullOrWhiteSpace(response.Error))
                {
                    result.Message = response.Error;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new ActionExecutionResultDto
            {
                ActionExecutionId = actionExecutionId,
                Success = false,
                ExitCode = 10,
                Message = ex.Message
            };
        }
    }

    public Task SubscribeEventsAsync(CancellationToken cancellationToken = default)
    {
        if (MockMode)
        {
            return Task.CompletedTask;
        }

        if (_subscriptionTask is not null && !_subscriptionTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        _subscriptionCts?.Cancel();
        _subscriptionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _subscriptionTask = Task.Run(async () =>
        {
            var delay = TimeSpan.FromSeconds(3);
            while (!_subscriptionCts.IsCancellationRequested)
            {
                try
                {
                    await GetLatestReportAsync(_subscriptionCts.Token);
                    await Task.Delay(TimeSpan.FromSeconds(8), _subscriptionCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SetConnection(false, ex.Message);
                    await Task.Delay(delay, _subscriptionCts.Token);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
            }
        }, _subscriptionCts.Token);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _subscriptionCts?.Cancel();
        if (_subscriptionTask is not null)
        {
            try
            {
                await _subscriptionTask;
            }
            catch
            {
            }
        }

        _requestLock.Dispose();
        _subscriptionCts?.Dispose();
    }

    private void UpdateReport(ScanReportDto report)
    {
        string identity = ReportStore.ComputeReportIdentity(report);
        if (identity == _lastReportIdentity)
        {
            return;
        }

        _lastReportIdentity = identity;
        _reportStore.Update(report);
    }

    private void SetConnection(bool connected, string error)
    {
        bool changed = connected != IsConnected || !string.Equals(error, LastError, StringComparison.Ordinal);
        IsConnected = connected;
        LastError = error;

        if (changed)
        {
            ConnectionChanged?.Invoke(this, connected);
        }
    }

    private ScanReportDto EnsureMockReport(bool refresh)
    {
        ScanReportDto current = _reportStore.CurrentReport;
        if (!refresh && current.Findings.Count > 0)
        {
            return current;
        }

        // Return an empty report – no fake data
        ScanReportDto empty = new()
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            HealthScore = 0,
            Findings = [],
            TopFindings = [],
            RecentlyResolved = [],
            RecentAutoFixLog = [],
            Timeline = [],
        };
        UpdateReport(empty);
        return empty;
    }

    private void SimulateMockAction(string actionId, string? findingId)
    {
        ScanReportDto report = ReportStore.Clone(EnsureMockReport(refresh: false));
        DateTimeOffset now = DateTimeOffset.UtcNow;

        FindingDto? resolved = null;
        if (!string.IsNullOrWhiteSpace(findingId))
        {
            resolved = report.Findings.FirstOrDefault(f => string.Equals(f.FindingId, findingId, StringComparison.OrdinalIgnoreCase));
        }

        if (resolved is not null)
        {
            report.Findings.RemoveAll(f => string.Equals(f.FindingId, resolved.FindingId, StringComparison.OrdinalIgnoreCase));
            resolved.State.IsResolvedRecently = true;
            resolved.State.ResolvedAtUtc = now;
            resolved.State.LastSeenUtc = now;
            report.RecentlyResolved.Insert(0, resolved);
            report.RecentlyResolved = report.RecentlyResolved
                .OrderByDescending(f => f.State.ResolvedAtUtc ?? DateTimeOffset.MinValue)
                .Take(8)
                .ToList();
            report.HealthScore = Math.Min(100, report.HealthScore + 2);
        }

        report.RecentAutoFixLog.Insert(0, new AutoFixLogItemDto
        {
            TimestampUtc = now,
            FindingId = resolved?.FindingId ?? findingId ?? $"mock.{actionId}",
            ActionId = actionId,
            ActionExecutionId = Guid.NewGuid().ToString("N"),
            Success = true,
            Message = "Mockup: Aktion wurde erfolgreich simuliert."
        });
        if (report.RecentAutoFixLog.Count > 20)
        {
            report.RecentAutoFixLog = report.RecentAutoFixLog.Take(20).ToList();
        }

        report.Timeline.Insert(0, new TimelineEventDto
        {
            TimestampUtc = now,
            Kind = "action_succeeded",
            Level = "success",
            Title = "Aktion simuliert",
            Message = $"Mockup: {actionId}",
            FindingId = resolved?.FindingId,
            ActionId = actionId
        });
        if (report.Timeline.Count > 80)
        {
            report.Timeline = report.Timeline.Take(80).ToList();
        }

        report.TopFindings = report.Findings
            .OrderByDescending(f => f.Priority)
            .Take(3)
            .ToList();
        report.GeneratedAtUtc = now;
        _reportStore.Update(report);
    }
}



