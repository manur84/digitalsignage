using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Event arguments for screenshot received event
/// </summary>
public class ScreenshotReceivedEventArgs : EventArgs
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ImageData { get; set; } = string.Empty;
    public string Format { get; set; } = "png";
}

/// <summary>
/// Background service that handles incoming WebSocket messages
/// </summary>
public class MessageHandlerService : BackgroundService
{
    private readonly ICommunicationService _communicationService;
    private readonly IClientService _clientService;
    private readonly LogStorageService _logStorageService;
    private readonly ILogger<MessageHandlerService> _logger;
    private readonly ConcurrentDictionary<Guid, Task> _messageHandlerTasks = new();

    /// <summary>
    /// Event raised when a screenshot is received from a client
    /// </summary>
    public static event EventHandler<ScreenshotReceivedEventArgs>? ScreenshotReceived;

    public MessageHandlerService(
        ICommunicationService communicationService,
        IClientService clientService,
        LogStorageService logStorageService,
        ILogger<MessageHandlerService> logger)
    {
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _logStorageService = logStorageService ?? throw new ArgumentNullException(nameof(logStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message Handler Service starting...");

        // Subscribe to message events
        _communicationService.MessageReceived += OnMessageReceived;

        // Subscribe to disconnect events to immediately mark clients offline
        _communicationService.ClientDisconnected += OnClientDisconnected;

        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Service is stopping
        }

        _logger.LogInformation("Message Handler Service stopped");
    }

    // Event handler queues work on background thread to avoid async void
    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        // Generate unique task ID
        var taskId = Guid.NewGuid();

        // Track the message handling task
        var handlerTask = Task.Run(async () =>
        {
            try
            {
                await HandleMessageAsync(e.ClientId, e.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from client {ClientId}", e.ClientId);
            }
            finally
            {
                // Remove from tracking when complete
                _messageHandlerTasks.TryRemove(taskId, out _);
            }
        });

        _messageHandlerTasks[taskId] = handlerTask;
    }

    /// <summary>
    /// Handle client disconnection - immediately mark client as offline
    /// </summary>
    private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
    {
        // Generate unique task ID
        var taskId = Guid.NewGuid();

        // Track the disconnect handling task
        var handlerTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Client {ClientId} disconnected from WebSocket - marking as offline", e.ClientId);

                await _clientService.UpdateClientStatusAsync(
                    e.ClientId,
                    ClientStatus.Offline);

                _logger.LogInformation("Client {ClientId} status updated to Offline", e.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling disconnect for client {ClientId}", e.ClientId);
            }
            finally
            {
                // Remove from tracking when complete
                _messageHandlerTasks.TryRemove(taskId, out _);
            }
        });

        _messageHandlerTasks[taskId] = handlerTask;
    }

    private async Task HandleMessageAsync(string clientId, Message message)
    {
        _logger.LogDebug("Handling message type {MessageType} from client {ClientId}", message.Type, clientId);

        switch (message.Type)
        {
            case "REGISTER":
                await HandleRegisterMessageAsync(clientId, message);
                break;

            case "HEARTBEAT":
                await HandleHeartbeatMessageAsync(clientId, message);
                break;

            case "STATUS_REPORT":
                await HandleStatusReportMessageAsync(clientId, message);
                break;

            case "LOG":
                await HandleLogMessageAsync(clientId, message);
                break;

            case "SCREENSHOT":
                await HandleScreenshotMessageAsync(clientId, message);
                break;

            case "UPDATE_CONFIG_RESPONSE":
                await HandleUpdateConfigResponseAsync(clientId, message);
                break;

            default:
                _logger.LogWarning("Unknown message type {MessageType} from client {ClientId}", message.Type, clientId);
                break;
        }
    }

    private async Task HandleRegisterMessageAsync(string clientId, Message message)
    {
        try
        {
            var registerMessage = DeserializeMessage<RegisterMessage>(message);
            if (registerMessage != null)
            {
                var result = await _clientService.RegisterClientAsync(registerMessage);
                if (result.IsSuccess && result.Value != null)
                {
                    var registeredClient = result.Value;
                    _logger.LogInformation("Client registered: {ClientId} from {IpAddress}",
                        registeredClient.Id, registeredClient.IpAddress);
                }
                else
                {
                    _logger.LogWarning("Client registration failed: {Error}", result.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling REGISTER message from client {ClientId}", clientId);
        }
    }

    private async Task HandleHeartbeatMessageAsync(string clientId, Message message)
    {
        try
        {
            var heartbeatMessage = DeserializeMessage<HeartbeatMessage>(message);
            if (heartbeatMessage != null)
            {
                await _clientService.UpdateClientStatusAsync(
                    heartbeatMessage.ClientId,
                    heartbeatMessage.Status,
                    heartbeatMessage.DeviceInfo);

                _logger.LogDebug("Heartbeat received from client {ClientId}", heartbeatMessage.ClientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HEARTBEAT message from client {ClientId}", clientId);
        }
    }

    private async Task HandleStatusReportMessageAsync(string clientId, Message message)
    {
        try
        {
            var statusMessage = DeserializeMessage<StatusReportMessage>(message);
            if (statusMessage != null)
            {
                await _clientService.UpdateClientStatusAsync(
                    statusMessage.ClientId,
                    statusMessage.Status,
                    statusMessage.DeviceInfo);

                if (!string.IsNullOrEmpty(statusMessage.ErrorMessage))
                {
                    _logger.LogWarning("Client {ClientId} reported error: {Error}",
                        statusMessage.ClientId, statusMessage.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling STATUS_REPORT message from client {ClientId}", clientId);
        }
    }

    private async Task HandleLogMessageAsync(string clientId, Message message)
    {
        try
        {
            var logMessage = DeserializeMessage<LogMessage>(message);
            if (logMessage != null)
            {
                // Get client info for better log display
                var clientResult = await _clientService.GetClientByIdAsync(logMessage.ClientId);
                var client = clientResult.IsSuccess ? clientResult.Value : null;

                var logEntry = new LogEntry
                {
                    ClientId = logMessage.ClientId,
                    ClientName = client?.Name ?? logMessage.ClientId,
                    Timestamp = logMessage.Timestamp,
                    Level = logMessage.Level,
                    Message = logMessage.Message,
                    Exception = logMessage.Exception,
                    Source = "Client"
                };

                _logStorageService.AddLog(logEntry);

                // Also log to server logs if it's warning or error
                // Skip asyncio internal errors as they're handled by the client
                bool isAsyncioInternalError = logMessage.Message?.Contains("Exception in callback") == true
                    || logMessage.Message?.Contains("_asyncio.TaskStepMethWrapper") == true;

                if (logMessage.Level >= Core.Models.LogLevel.Warning && !isAsyncioInternalError)
                {
                    _logger.LogWarning("Client {ClientId} [{Level}]: {Message}",
                        logMessage.ClientId, logMessage.Level, logMessage.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling LOG message from client {ClientId}", clientId);
        }
    }

    private async Task HandleScreenshotMessageAsync(string clientId, Message message)
    {
        try
        {
            _logger.LogInformation("=== HandleScreenshotMessageAsync START ===");

            var screenshotMessage = DeserializeMessage<ScreenshotMessage>(message);
            if (screenshotMessage != null)
            {
                _logger.LogInformation("Screenshot message deserialized successfully");
                _logger.LogInformation("ClientId: {ClientId}", screenshotMessage.ClientId);
                _logger.LogInformation("ImageData length: {Length} characters", screenshotMessage.ImageData?.Length ?? 0);
                _logger.LogInformation("Format: {Format}", screenshotMessage.Format);

                // Get client name for better display
                var clientResult = await _clientService.GetClientByIdAsync(screenshotMessage.ClientId);
                var clientName = clientResult.IsSuccess && clientResult.Value != null
                    ? clientResult.Value.Name
                    : screenshotMessage.ClientId;
                _logger.LogInformation("Client name resolved: {ClientName}", clientName);

                // Raise event to notify UI
                if (!string.IsNullOrWhiteSpace(screenshotMessage.ImageData))
                {
                    var eventArgs = new ScreenshotReceivedEventArgs
                    {
                        ClientId = screenshotMessage.ClientId,
                        ClientName = clientName,
                        ImageData = screenshotMessage.ImageData,
                        Format = screenshotMessage.Format
                    };

                    _logger.LogInformation("Invoking ScreenshotReceived event...");
                    ScreenshotReceived?.Invoke(this, eventArgs);
                    _logger.LogInformation("Screenshot event raised successfully for client {ClientName}", clientName);
                }
                else
                {
                    _logger.LogWarning("Screenshot from client {ClientId} has no image data", screenshotMessage.ClientId);
                }
            }
            else
            {
                _logger.LogError("Failed to deserialize screenshot message - result was null");
            }

            _logger.LogInformation("=== HandleScreenshotMessageAsync END ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== HandleScreenshotMessageAsync FAILED === Error handling SCREENSHOT message from client {ClientId}", clientId);
        }
    }

    private Task HandleUpdateConfigResponseAsync(string clientId, Message message)
    {
        try
        {
            var responseMessage = DeserializeMessage<UpdateConfigResponseMessage>(message);
            if (responseMessage != null)
            {
                if (responseMessage.Success)
                {
                    _logger.LogInformation("Client {ClientId} successfully updated configuration", clientId);
                }
                else
                {
                    _logger.LogWarning("Client {ClientId} failed to update configuration: {Error}",
                        clientId,
                        responseMessage.ErrorMessage ?? "Unknown error");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling UPDATE_CONFIG_RESPONSE message from client {ClientId}", clientId);
        }

        return Task.CompletedTask;
    }

    private T? DeserializeMessage<T>(Message message) where T : Message
    {
        try
        {
            // Re-serialize and deserialize to get proper type
            var json = JsonConvert.SerializeObject(message);
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize message of type {MessageType}", typeof(T).Name);
            return null;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Message Handler Service stopping...");

        // 1. Unsubscribe from events to stop receiving new messages
        _communicationService.MessageReceived -= OnMessageReceived;
        _communicationService.ClientDisconnected -= OnClientDisconnected;

        // 2. Wait for all pending message handler tasks to complete (with 10s timeout)
        var pendingTasks = _messageHandlerTasks.Values.ToArray();
        if (pendingTasks.Length > 0)
        {
            try
            {
                _logger.LogInformation("Waiting for {Count} message handler tasks to complete", pendingTasks.Length);
                await Task.WhenAll(pendingTasks).WaitAsync(TimeSpan.FromSeconds(10));
                _logger.LogInformation("All message handler tasks completed gracefully");
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("{Count} message handler tasks did not complete within timeout", pendingTasks.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for message handler tasks to complete");
            }
        }

        // 3. Clear the task dictionary
        _messageHandlerTasks.Clear();

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Message Handler Service stopped");
    }
}
