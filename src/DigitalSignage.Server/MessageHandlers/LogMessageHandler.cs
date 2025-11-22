using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles Log messages from Pi clients
/// Moved from MessageHandlerService.HandleLogMessageAsync
/// </summary>
public class LogMessageHandler : MessageHandlerBase
{
    private readonly ILogger<LogMessageHandler> _logger;
    private readonly IClientService _clientService;
    private readonly LogStorageService _logStorageService;

    public override string MessageType => MessageTypes.Log;

    public LogMessageHandler(
        ILogger<LogMessageHandler> logger,
        IClientService clientService,
        LogStorageService logStorageService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _logStorageService = logStorageService ?? throw new ArgumentNullException(nameof(logStorageService));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var logMessage = message as Core.Models.LogMessage;
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
            _logger.LogError(ex, "Error handling LOG message from client {ClientId}", connectionId);
        }
    }
}
