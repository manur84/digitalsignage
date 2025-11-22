using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace DigitalSignage.Server.Helpers;

/// <summary>
/// Central message validation helper to ensure consistent validation across all message handlers
/// </summary>
public class MessageValidationHelper
{
    private readonly ILogger<MessageValidationHelper> _logger;

    // Maximum message size (50 MB - should match ServerSettings.MaxMessageSize)
    private const int MaxMessageSizeBytes = 50 * 1024 * 1024;

    // Valid message types for Raspberry Pi clients
    private static readonly string[] ValidDeviceMessageTypes =
    {
        MessageTypes.Register,
        MessageTypes.Heartbeat,
        MessageTypes.StatusReport,
        MessageTypes.Screenshot,
        MessageTypes.Log,
        MessageTypes.UpdateConfigResponse
    };

    // Valid message types for Mobile Apps
    private static readonly string[] ValidMobileAppMessageTypes =
    {
        MobileAppMessageTypes.AppRegister,
        MobileAppMessageTypes.AppHeartbeat,
        MobileAppMessageTypes.RequestClientList,
        MobileAppMessageTypes.SendCommand,
        MobileAppMessageTypes.AssignLayout,
        MobileAppMessageTypes.RequestScreenshot,
        MobileAppMessageTypes.RequestLayoutList
    };

    public MessageValidationHelper(ILogger<MessageValidationHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validate message size
    /// </summary>
    public ValidationResult ValidateMessageSize(int messageSize, string connectionId)
    {
        if (messageSize <= 0)
        {
            _logger.LogWarning("Invalid message size {Size} from {ConnectionId}", messageSize, connectionId);
            return ValidationResult.Failure("Message size must be greater than zero");
        }

        if (messageSize > MaxMessageSizeBytes)
        {
            _logger.LogError("Message size {Size} exceeds maximum {MaxSize} from {ConnectionId}",
                messageSize, MaxMessageSizeBytes, connectionId);
            return ValidationResult.Failure($"Message size {messageSize / (1024 * 1024)} MB exceeds maximum {MaxMessageSizeBytes / (1024 * 1024)} MB");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate message type is known and allowed
    /// </summary>
    public ValidationResult ValidateMessageType(string? messageType, string connectionId, bool isMobileApp = false)
    {
        if (string.IsNullOrWhiteSpace(messageType))
        {
            _logger.LogWarning("Message from {ConnectionId} missing 'type' field", connectionId);
            return ValidationResult.Failure("Message type is required");
        }

        var validTypes = isMobileApp ? ValidMobileAppMessageTypes : ValidDeviceMessageTypes;

        if (!validTypes.Contains(messageType, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Unknown message type '{MessageType}' from {ConnectionId} (isMobileApp: {IsMobileApp})",
                messageType, connectionId, isMobileApp);
            return ValidationResult.Failure($"Unknown message type: {messageType}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate Register message has required fields
    /// </summary>
    public ValidationResult ValidateRegisterMessage(RegisterMessage? message, string connectionId)
    {
        if (message == null)
        {
            _logger.LogWarning("Null RegisterMessage from {ConnectionId}", connectionId);
            return ValidationResult.Failure("Register message is null");
        }

        if (string.IsNullOrWhiteSpace(message.ClientId))
        {
            _logger.LogWarning("RegisterMessage missing ClientId from {ConnectionId}", connectionId);
            return ValidationResult.Failure("ClientId is required");
        }

        if (string.IsNullOrWhiteSpace(message.Token))
        {
            _logger.LogWarning("RegisterMessage missing Token from {ConnectionId}", connectionId);
            return ValidationResult.Failure("Token is required");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate AppRegister message has required fields
    /// </summary>
    public ValidationResult ValidateAppRegisterMessage(AppRegisterMessage? message, string connectionId)
    {
        if (message == null)
        {
            _logger.LogWarning("Null AppRegisterMessage from {ConnectionId}", connectionId);
            return ValidationResult.Failure("AppRegister message is null");
        }

        if (string.IsNullOrWhiteSpace(message.DeviceName))
        {
            _logger.LogWarning("AppRegisterMessage missing DeviceName from {ConnectionId}", connectionId);
            return ValidationResult.Failure("DeviceName is required");
        }

        if (string.IsNullOrWhiteSpace(message.DeviceIdentifier))
        {
            _logger.LogWarning("AppRegisterMessage missing DeviceIdentifier from {ConnectionId}", connectionId);
            return ValidationResult.Failure("DeviceIdentifier is required");
        }

        if (string.IsNullOrWhiteSpace(message.Platform))
        {
            _logger.LogWarning("AppRegisterMessage missing Platform from {ConnectionId}", connectionId);
            return ValidationResult.Failure("Platform is required");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate Command message has required fields
    /// </summary>
    public ValidationResult ValidateCommandMessage(CommandMessage? message, string connectionId)
    {
        if (message == null)
        {
            _logger.LogWarning("Null CommandMessage from {ConnectionId}", connectionId);
            return ValidationResult.Failure("Command message is null");
        }

        if (string.IsNullOrWhiteSpace(message.Command))
        {
            _logger.LogWarning("CommandMessage missing Command from {ConnectionId}", connectionId);
            return ValidationResult.Failure("Command is required");
        }

        // Validate command is one of the allowed commands
        var validCommands = new[] { "Restart", "Screenshot", "VolumeUp", "VolumeDown", "ScreenOn", "ScreenOff", "Update" };
        if (!validCommands.Contains(message.Command, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid command '{Command}' from {ConnectionId}", message.Command, connectionId);
            return ValidationResult.Failure($"Invalid command: {message.Command}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate generic message has required base fields
    /// </summary>
    public ValidationResult ValidateMessageBase(Message? message, string connectionId)
    {
        if (message == null)
        {
            _logger.LogWarning("Null message from {ConnectionId}", connectionId);
            return ValidationResult.Failure("Message is null");
        }

        if (string.IsNullOrWhiteSpace(message.Type))
        {
            _logger.LogWarning("Message missing Type field from {ConnectionId}", connectionId);
            return ValidationResult.Failure("Message type is required");
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true);
    public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
