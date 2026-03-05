using System.IO.Pipes;
using System.Text.Json;
using PCWachter.Contracts;

namespace AgentService.Interop;

internal sealed class IpcServerHostedService : BackgroundService
{
    private const string PipeName = "PCWaechter.Service.Ipc";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly Runtime.ScanCoordinator _scanCoordinator;
    private readonly ILogger<IpcServerHostedService> _logger;

    public IpcServerHostedService(Runtime.ScanCoordinator scanCoordinator, ILogger<IpcServerHostedService> logger)
    {
        _scanCoordinator = scanCoordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IPC server listening on pipe {PipeName}", PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(stoppingToken);
                await HandleClientAsync(server, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IPC client handling failed");
            }
        }
    }

    private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream) { AutoFlush = true };

        string? line = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        IpcResponseDto response;

        try
        {
            IpcRequestDto? request = JsonSerializer.Deserialize<IpcRequestDto>(line, SerializerOptions);
            if (request is null)
            {
                response = new IpcResponseDto { Ok = false, Error = "invalid request" };
            }
            else
            {
                response = await HandleRequestAsync(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            response = new IpcResponseDto
            {
                Ok = false,
                Error = ex.Message
            };
        }

        string payload = JsonSerializer.Serialize(response, SerializerOptions);
        await writer.WriteLineAsync(payload);
    }

    private async Task<IpcResponseDto> HandleRequestAsync(IpcRequestDto request, CancellationToken cancellationToken)
    {
        var response = new IpcResponseDto
        {
            RequestId = request.RequestId,
            Ok = true
        };

        switch (request.Type)
        {
            case IpcMessageTypes.GetReport:
            {
                var report = _scanCoordinator.GetCurrentReport();
                response.PayloadJson = JsonSerializer.Serialize(report, SerializerOptions);
                break;
            }

            case IpcMessageTypes.TriggerScan:
            {
                var report = await _scanCoordinator.RunScanAsync(cancellationToken);
                response.PayloadJson = JsonSerializer.Serialize(report, SerializerOptions);
                break;
            }

            case IpcMessageTypes.Subscribe:
            {
                // Event streaming is currently implemented client-side via polling.
                response.PayloadJson = JsonSerializer.Serialize(new { subscribed = true, mode = "polling" }, SerializerOptions);
                break;
            }

            case IpcMessageTypes.ExecuteAction:
            {
                if (string.IsNullOrWhiteSpace(request.PayloadJson))
                {
                    response.Ok = false;
                    response.Error = "missing payload";
                    break;
                }

                var actionRequest = JsonSerializer.Deserialize<ExecuteActionRequestDto>(request.PayloadJson, SerializerOptions);
                if (actionRequest is null)
                {
                    response.Ok = false;
                    response.Error = "invalid action payload";
                    break;
                }

                var result = await _scanCoordinator.ExecuteActionAsync(
                    actionRequest.ActionId,
                    actionRequest.SimulationMode,
                    actionRequest.Parameters,
                    cancellationToken);
                response.Ok = result.Success;
                response.PayloadJson = JsonSerializer.Serialize(result, SerializerOptions);
                response.Error = result.Success ? null : result.Message;
                break;
            }

            case IpcMessageTypes.SetFindingState:
            {
                if (string.IsNullOrWhiteSpace(request.PayloadJson))
                {
                    response.Ok = false;
                    response.Error = "missing payload";
                    break;
                }

                var stateRequest = JsonSerializer.Deserialize<SetFindingStateRequestDto>(request.PayloadJson, SerializerOptions);
                if (stateRequest is null)
                {
                    response.Ok = false;
                    response.Error = "invalid state payload";
                    break;
                }

                await _scanCoordinator.SetFindingStateAsync(stateRequest, cancellationToken);
                break;
            }

            case IpcMessageTypes.GetFeatureConfig:
            {
                FeatureConfigDto config = await _scanCoordinator.GetFeatureConfigAsync(cancellationToken);
                response.PayloadJson = JsonSerializer.Serialize(config, SerializerOptions);
                break;
            }

            case IpcMessageTypes.SetFeatureConfig:
            {
                if (string.IsNullOrWhiteSpace(request.PayloadJson))
                {
                    response.Ok = false;
                    response.Error = "missing payload";
                    break;
                }

                SetFeatureConfigRequestDto? configRequest = JsonSerializer.Deserialize<SetFeatureConfigRequestDto>(request.PayloadJson, SerializerOptions);
                if (configRequest is null)
                {
                    response.Ok = false;
                    response.Error = "invalid config payload";
                    break;
                }

                await _scanCoordinator.SetFeatureConfigAsync(configRequest, cancellationToken);
                break;
            }

            case IpcMessageTypes.CreateBaseline:
            {
                CreateBaselineRequestDto baselineRequest = string.IsNullOrWhiteSpace(request.PayloadJson)
                    ? new CreateBaselineRequestDto()
                    : JsonSerializer.Deserialize<CreateBaselineRequestDto>(request.PayloadJson, SerializerOptions) ?? new CreateBaselineRequestDto();

                CreateBaselineResultDto result = await _scanCoordinator.CreateBaselineAsync(baselineRequest, cancellationToken);
                response.Ok = result.Success;
                response.Error = result.Success ? null : result.Message;
                response.PayloadJson = JsonSerializer.Serialize(result, SerializerOptions);
                break;
            }

            case IpcMessageTypes.RollbackAction:
            {
                if (string.IsNullOrWhiteSpace(request.PayloadJson))
                {
                    response.Ok = false;
                    response.Error = "missing payload";
                    break;
                }

                RollbackActionRequestDto? rollbackRequest = JsonSerializer.Deserialize<RollbackActionRequestDto>(request.PayloadJson, SerializerOptions);
                if (rollbackRequest is null)
                {
                    response.Ok = false;
                    response.Error = "invalid rollback payload";
                    break;
                }

                ActionExecutionResultDto rollbackResult = await _scanCoordinator.RollbackActionAsync(rollbackRequest.ActionExecutionId, cancellationToken);
                response.Ok = rollbackResult.Success;
                response.Error = rollbackResult.Success ? null : rollbackResult.Message;
                response.PayloadJson = JsonSerializer.Serialize(rollbackResult, SerializerOptions);
                break;
            }

            case IpcMessageTypes.RunSafeMaintenance:
            {
                MaintenanceRunResultDto result = await _scanCoordinator.RunSafeMaintenanceAsync(cancellationToken);
                response.Ok = result.Steps.All(s => s.Success || s.Skipped);
                response.PayloadJson = JsonSerializer.Serialize(result, SerializerOptions);
                if (!response.Ok)
                {
                    response.Error = result.Summary;
                }
                break;
            }

            default:
                response.Ok = false;
                response.Error = $"unknown message type: {request.Type}";
                break;
        }

        return response;
    }
}
