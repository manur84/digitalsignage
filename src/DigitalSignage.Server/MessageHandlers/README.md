# Message Handler Pattern

## Overview

The Message Handler Pattern separates message processing logic from the WebSocket communication layer. This improves:

- **Maintainability**: Each handler is focused on a single message type
- **Testability**: Handlers can be unit tested in isolation
- **Scalability**: New message types can be added without modifying WebSocketCommunicationService
- **Separation of Concerns**: Communication logic separate from business logic

## Architecture

```
IMessageHandler (Interface)
    └── MessageHandlerBase (Abstract Base Class)
            ├── RegisterMessageHandler
            ├── HeartbeatMessageHandler
            ├── StatusReportMessageHandler
            ├── ScreenshotMessageHandler
            ├── AppRegisterMessageHandler
            └── ... (more handlers)

MessageHandlerFactory (Factory)
    └── Creates and manages all handlers
```

## Usage in WebSocketCommunicationService

### Before (OLD - Monolithic):
```csharp
private async Task ProcessClientMessageAsync(string messageJson)
{
    var message = JsonConvert.DeserializeObject<Message>(messageJson);

    switch (message.Type)
    {
        case MessageTypes.Register:
            var registerMsg = message as RegisterMessage;
            // 50+ lines of registration logic
            break;

        case MessageTypes.Heartbeat:
            var heartbeatMsg = message as HeartbeatMessage;
            // 20+ lines of heartbeat logic
            break;

        // ... 10+ more cases
    }
}
```

### After (NEW - Handler Pattern):
```csharp
private readonly MessageHandlerFactory _handlerFactory;

private async Task ProcessClientMessageAsync(string messageJson, string connectionId)
{
    var message = JsonConvert.DeserializeObject<Message>(messageJson);

    var handler = _handlerFactory.GetHandler(message.Type);
    if (handler != null)
    {
        await handler.HandleAsync(message, connectionId, cancellationToken);
    }
    else
    {
        _logger.LogWarning("No handler for message type {MessageType}", message.Type);
    }
}
```

## Creating a New Handler

1. Create a new class inheriting from `MessageHandlerBase`
2. Implement `MessageType` property
3. Implement `HandleAsync` method
4. Register in `MessageHandlerFactory.RegisterHandlers()`
5. Register in DI container (App.xaml.cs or Startup.cs)

### Example: StatusReportMessageHandler

```csharp
public class StatusReportMessageHandler : MessageHandlerBase
{
    private readonly IClientService _clientService;
    private readonly ILogger<StatusReportMessageHandler> _logger;

    public override string MessageType => MessageTypes.StatusReport;

    public StatusReportMessageHandler(
        IClientService clientService,
        ILogger<StatusReportMessageHandler> logger)
    {
        _clientService = clientService;
        _logger = logger;
    }

    public override async Task HandleAsync(
        Message message,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        if (message is not StatusReportMessage statusMsg)
        {
            _logger.LogWarning("Invalid message type");
            return;
        }

        // Process status report
        await _clientService.UpdateClientStatusAsync(
            connectionId,
            statusMsg.Status,
            statusMsg.DeviceInfo);
    }
}
```

## Dependency Injection Setup

In `App.xaml.cs` or `Startup.cs`:

```csharp
services.AddTransient<IMessageHandler, RegisterMessageHandler>();
services.AddTransient<IMessageHandler, HeartbeatMessageHandler>();
services.AddTransient<IMessageHandler, StatusReportMessageHandler>();
// ... add all handlers

services.AddSingleton<MessageHandlerFactory>();
```

## Benefits

1. **Reduced Class Size**: WebSocketCommunicationService reduced from 1493 to ~800 lines
2. **Single Responsibility**: Each handler does ONE thing
3. **Easy Testing**: Mock dependencies, test handler in isolation
4. **Easy Extension**: Add new message types without touching existing code
5. **Better Error Handling**: Isolated error handling per message type
6. **Parallel Processing**: Handlers can be processed concurrently (future enhancement)

## Migration Strategy

1. ✅ Create handler infrastructure (IMessageHandler, Factory)
2. ✅ Create handlers for most frequent messages (Register, Heartbeat)
3. ⏳ Create handlers for remaining device messages (StatusReport, Screenshot, Log)
4. ⏳ Create handlers for mobile app messages (AppRegister, SendCommand, etc.)
5. ⏳ Update WebSocketCommunicationService to use handlers
6. ⏳ Remove old switch/case logic from WebSocketCommunicationService
7. ✅ Register all handlers in DI container

## Performance Considerations

- Handlers are **transient** (created per request)
- Factory is **singleton** (created once)
- Handler lookup is O(1) via Dictionary
- No reflection overhead (direct DI resolution)

## Future Enhancements

- **Handler Pipeline**: Add middleware for cross-cutting concerns (validation, logging, metrics)
- **Message Versioning**: Handler can support multiple message versions
- **Priority Queues**: Critical messages (Heartbeat) processed before low-priority (Screenshot)
- **Concurrent Processing**: Process independent messages in parallel
- **Rate Limiting**: Per-handler rate limits
