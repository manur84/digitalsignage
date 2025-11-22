using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Factory for creating message handlers
/// Implements Factory Pattern + Service Locator
/// </summary>
public class MessageHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageHandlerFactory> _logger;
    private readonly Dictionary<string, Type> _handlerTypes;

    public MessageHandlerFactory(
        IServiceProvider serviceProvider,
        ILogger<MessageHandlerFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlerTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        RegisterHandlers();
    }

    /// <summary>
    /// Register all available message handlers
    /// </summary>
    private void RegisterHandlers()
    {
        // Register device message handlers
        RegisterHandler<RegisterMessageHandler>();
        RegisterHandler<HeartbeatMessageHandler>();
        // Add more handlers as they are created:
        // RegisterHandler<StatusReportMessageHandler>();
        // RegisterHandler<ScreenshotMessageHandler>();
        // RegisterHandler<LogMessageHandler>();

        // Register mobile app message handlers
        // RegisterHandler<AppRegisterMessageHandler>();
        // RegisterHandler<AppHeartbeatMessageHandler>();
        // RegisterHandler<RequestClientListMessageHandler>();
        // RegisterHandler<SendCommandMessageHandler>();

        _logger.LogInformation("Registered {Count} message handlers", _handlerTypes.Count);
    }

    /// <summary>
    /// Register a handler type
    /// </summary>
    private void RegisterHandler<THandler>() where THandler : IMessageHandler
    {
        var handler = _serviceProvider.GetService<THandler>();
        if (handler != null)
        {
            _handlerTypes[handler.MessageType] = typeof(THandler);
            _logger.LogDebug("Registered handler {HandlerType} for message type {MessageType}",
                typeof(THandler).Name, handler.MessageType);
        }
        else
        {
            _logger.LogWarning("Handler {HandlerType} not found in DI container", typeof(THandler).Name);
        }
    }

    /// <summary>
    /// Get handler for the specified message type
    /// </summary>
    public IMessageHandler? GetHandler(string messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
        {
            _logger.LogWarning("Cannot get handler for null or empty message type");
            return null;
        }

        if (_handlerTypes.TryGetValue(messageType, out var handlerType))
        {
            try
            {
                var handler = _serviceProvider.GetService(handlerType) as IMessageHandler;
                if (handler == null)
                {
                    _logger.LogError("Failed to resolve handler {HandlerType} from DI", handlerType.Name);
                }
                return handler;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating handler for message type {MessageType}", messageType);
                return null;
            }
        }

        _logger.LogDebug("No handler registered for message type {MessageType}", messageType);
        return null;
    }

    /// <summary>
    /// Check if a handler is registered for the message type
    /// </summary>
    public bool HasHandler(string messageType)
    {
        return !string.IsNullOrWhiteSpace(messageType) &&
               _handlerTypes.ContainsKey(messageType);
    }

    /// <summary>
    /// Get all registered message types
    /// </summary>
    public IEnumerable<string> GetRegisteredMessageTypes()
    {
        return _handlerTypes.Keys.ToList();
    }
}
