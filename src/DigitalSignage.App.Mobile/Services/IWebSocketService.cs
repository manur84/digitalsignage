namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Service for WebSocket communication with the Digital Signage server.
/// </summary>
public interface IWebSocketService
{
	/// <summary>
	/// Occurs when the connection state changes.
	/// </summary>
	event EventHandler<ConnectionState>? ConnectionStateChanged;

	/// <summary>
	/// Occurs when a message is received from the server.
	/// </summary>
	event EventHandler<string>? MessageReceived;

	/// <summary>
	/// Gets the current connection state.
	/// </summary>
	ConnectionState State { get; }

	/// <summary>
	/// Connects to the WebSocket server.
	/// </summary>
	Task ConnectAsync(string webSocketUrl, CancellationToken cancellationToken = default);

	/// <summary>
	/// Disconnects from the WebSocket server.
	/// </summary>
	Task DisconnectAsync();

	/// <summary>
	/// Sends a message to the server.
	/// </summary>
	Task SendAsync(string message, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a JSON object to the server.
	/// </summary>
	Task SendJsonAsync<T>(T data, CancellationToken cancellationToken = default);
}

/// <summary>
/// WebSocket connection states.
/// </summary>
public enum ConnectionState
{
	/// <summary>
	/// Not connected.
	/// </summary>
	Disconnected,

	/// <summary>
	/// Connecting to server.
	/// </summary>
	Connecting,

	/// <summary>
	/// Connected to server.
	/// </summary>
	Connected,

	/// <summary>
	/// Disconnecting from server.
	/// </summary>
	Disconnecting,

	/// <summary>
	/// Connection failed.
	/// </summary>
	Failed
}
