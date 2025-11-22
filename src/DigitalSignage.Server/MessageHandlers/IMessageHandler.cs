using DigitalSignage.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.MessageHandlers;

/// <summary>
/// Interface for handling specific message types
/// Implements Strategy Pattern for message processing
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Message type this handler processes
    /// </summary>
    string MessageType { get; }

    /// <summary>
    /// Process the message
    /// </summary>
    /// <param name="message">The message to process</param>
    /// <param name="connectionId">WebSocket connection ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if this handler can process the given message
    /// </summary>
    bool CanHandle(string messageType);
}

/// <summary>
/// Base class for message handlers providing common functionality
/// </summary>
public abstract class MessageHandlerBase : IMessageHandler
{
    public abstract string MessageType { get; }

    public virtual bool CanHandle(string messageType)
    {
        return string.Equals(MessageType, messageType, System.StringComparison.OrdinalIgnoreCase);
    }

    public abstract Task HandleAsync(Message message, string connectionId, CancellationToken cancellationToken = default);
}
