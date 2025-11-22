using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Handles Screenshot messages from Pi clients
/// Moved from MessageHandlerService.HandleScreenshotMessageAsync
/// </summary>
public class ScreenshotMessageHandler : MessageHandlerBase
{
    private readonly ILogger<ScreenshotMessageHandler> _logger;
    private readonly IClientService _clientService;

    public override string MessageType => MessageTypes.Screenshot;

    /// <summary>
    /// Event raised when a screenshot is received from a client
    /// </summary>
    public static event EventHandler<ScreenshotReceivedEventArgs>? ScreenshotReceived;

    public ScreenshotMessageHandler(
        ILogger<ScreenshotMessageHandler> logger,
        IClientService clientService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
    }

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("=== HandleScreenshotMessageAsync START ===");

            var screenshotMessage = message as ScreenshotMessage;
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
            _logger.LogError(ex, "=== HandleScreenshotMessageAsync FAILED === Error handling SCREENSHOT message from client {ClientId}", connectionId);
        }
    }
}
