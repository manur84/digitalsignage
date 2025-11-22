using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Implementation of WebSocket service for real-time communication with the server.
/// </summary>
public class WebSocketService : IWebSocketService, IDisposable
{
	private const int ReceiveBufferSize = 8192;
	private const int ReconnectDelayMs = 5000;
	private const int MaxReconnectAttempts = 5;

	private ClientWebSocket? _webSocket;
	private CancellationTokenSource? _receiveLoopCts;
	private Task? _receiveLoopTask;
	private string? _webSocketUrl;
	private int _reconnectAttempts;
	private readonly ConcurrentDictionary<Guid, TaskCompletionSource<string?>> _screenshotRequests = new();

	/// <inheritdoc/>
	public event EventHandler<ConnectionState>? ConnectionStateChanged;

	/// <inheritdoc/>
	public event EventHandler<string>? MessageReceived;

	private ConnectionState _state = ConnectionState.Disconnected;

	/// <inheritdoc/>
	public ConnectionState State
	{
		get => _state;
		private set
		{
			if (_state != value)
			{
				_state = value;
				Console.WriteLine($"WebSocket state changed to: {value}");
				ConnectionStateChanged?.Invoke(this, value);
			}
		}
	}

	/// <inheritdoc/>
	public async Task ConnectAsync(string webSocketUrl, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(webSocketUrl))
			throw new ArgumentException("WebSocket URL cannot be null or empty", nameof(webSocketUrl));

		if (State == ConnectionState.Connected || State == ConnectionState.Connecting)
		{
			Console.WriteLine("Already connected or connecting");
			return;
		}

		_webSocketUrl = webSocketUrl;
		_reconnectAttempts = 0;

		await ConnectInternalAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public async Task DisconnectAsync()
	{
		if (State == ConnectionState.Disconnected)
			return;

		State = ConnectionState.Disconnecting;

		try
		{
			// Stop receive loop
			_receiveLoopCts?.Cancel();

			if (_webSocket?.State == WebSocketState.Open)
			{
				await _webSocket.CloseAsync(
					WebSocketCloseStatus.NormalClosure,
					"Disconnecting",
					CancellationToken.None);
			}

			// Wait for receive loop to finish
			if (_receiveLoopTask != null)
			{
				await _receiveLoopTask;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error during disconnect: {ex.Message}");
		}
		finally
		{
			CleanupWebSocket();
			State = ConnectionState.Disconnected;
		}
	}

	/// <inheritdoc/>
	public async Task SendAsync(string message, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(message))
			throw new ArgumentException("Message cannot be null or empty", nameof(message));

		if (State != ConnectionState.Connected)
			throw new InvalidOperationException("Not connected to server");

		if (_webSocket == null)
			throw new InvalidOperationException("WebSocket is null");

		try
		{
			var bytes = Encoding.UTF8.GetBytes(message);
			var buffer = new ArraySegment<byte>(bytes);

			await _webSocket.SendAsync(
				buffer,
				WebSocketMessageType.Text,
				endOfMessage: true,
				cancellationToken);

			Console.WriteLine($"Sent message: {message.Substring(0, Math.Min(100, message.Length))}...");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error sending message: {ex.Message}");
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task SendJsonAsync<T>(T data, CancellationToken cancellationToken = default)
	{
		if (data == null)
			throw new ArgumentNullException(nameof(data));

		var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		});

		await SendAsync(json, cancellationToken);
	}

	private async Task ConnectInternalAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(_webSocketUrl))
			throw new InvalidOperationException("WebSocket URL not set");

		State = ConnectionState.Connecting;

		try
		{
			CleanupWebSocket();

			_webSocket = new ClientWebSocket();

			// Configure SSL options (allow self-signed certificates for development)
			_webSocket.Options.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;

			Console.WriteLine($"Connecting to WebSocket: {_webSocketUrl}");
			await _webSocket.ConnectAsync(new Uri(_webSocketUrl), cancellationToken);

			State = ConnectionState.Connected;
			_reconnectAttempts = 0;

			// Start receive loop
			_receiveLoopCts = new CancellationTokenSource();
			_receiveLoopTask = ReceiveLoopAsync(_receiveLoopCts.Token);

			Console.WriteLine("WebSocket connected successfully");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Connection failed: {ex.Message}");
			State = ConnectionState.Failed;

			// Attempt reconnect
			if (_reconnectAttempts < MaxReconnectAttempts)
			{
				_reconnectAttempts++;
				Console.WriteLine($"Reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts} in {ReconnectDelayMs}ms");
				await Task.Delay(ReconnectDelayMs, cancellationToken);
				await ConnectInternalAsync(cancellationToken);
			}
			else
			{
				Console.WriteLine("Max reconnect attempts reached");
				throw;
			}
		}
	}

	private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
	{
		var buffer = new byte[ReceiveBufferSize];
		var messageBuilder = new StringBuilder();

		try
		{
			while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
			{
				var result = await _webSocket.ReceiveAsync(
					new ArraySegment<byte>(buffer),
					cancellationToken);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					Console.WriteLine($"Server requested close: {result.CloseStatus}");
					await DisconnectAsync();
					break;
				}

				if (result.MessageType == WebSocketMessageType.Text)
				{
					var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
					messageBuilder.Append(chunk);

					if (result.EndOfMessage)
					{
						var message = messageBuilder.ToString();
						messageBuilder.Clear();

						Console.WriteLine($"Received message: {message.Substring(0, Math.Min(100, message.Length))}...");

						// Process message internally first (for screenshot responses, etc.)
						ProcessMessage(message);

						// Then notify external listeners
						MessageReceived?.Invoke(this, message);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine("Receive loop cancelled");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in receive loop: {ex.Message}");
			State = ConnectionState.Failed;

			// Attempt reconnect
			if (!cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(ReconnectDelayMs, cancellationToken);
				await ConnectInternalAsync(cancellationToken);
			}
		}
	}

	private void CleanupWebSocket()
	{
		_receiveLoopCts?.Cancel();
		_receiveLoopCts?.Dispose();
		_receiveLoopCts = null;

		_webSocket?.Dispose();
		_webSocket = null;

		_receiveLoopTask = null;
	}

	/// <inheritdoc/>
	public async Task SendCommandAsync(Guid deviceId, string command, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
	{
		if (deviceId == Guid.Empty)
			throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));

		if (string.IsNullOrWhiteSpace(command))
			throw new ArgumentException("Command cannot be null or empty", nameof(command));

		var message = new SendCommandMessage
		{
			TargetDeviceId = deviceId,
			Command = command,
			Parameters = parameters
		};

		await SendJsonAsync(message, cancellationToken);
		Console.WriteLine($"Sent command '{command}' to device {deviceId}");
	}

	/// <inheritdoc/>
	public async Task<string?> RequestScreenshotAsync(Guid deviceId, int timeoutSeconds = 10, CancellationToken cancellationToken = default)
	{
		if (deviceId == Guid.Empty)
			throw new ArgumentException("Device ID cannot be empty", nameof(deviceId));

		if (timeoutSeconds <= 0)
			throw new ArgumentException("Timeout must be greater than 0", nameof(timeoutSeconds));

		// Create a task completion source for this screenshot request
		var tcs = new TaskCompletionSource<string?>();
		_screenshotRequests[deviceId] = tcs;

		try
		{
			// Send screenshot request
			var message = new RequestScreenshotMessage
			{
				DeviceId = deviceId
			};

			await SendJsonAsync(message, cancellationToken);
			Console.WriteLine($"Sent screenshot request to device {deviceId}");

			// Wait for response with timeout
			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			linkedCts.Token.Register(() =>
			{
				tcs.TrySetCanceled();
			});

			var result = await tcs.Task;
			Console.WriteLine($"Received screenshot response from device {deviceId}: {(result != null ? "Success" : "Failed")}");
			return result;
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine($"Screenshot request timed out for device {deviceId}");
			return null;
		}
		finally
		{
			_screenshotRequests.TryRemove(deviceId, out _);
		}
	}

	/// <summary>
	/// Processes received messages and handles screenshot responses.
	/// </summary>
	private void ProcessMessage(string message)
	{
		try
		{
			// Try to deserialize as a generic message to get the type
			var jsonDoc = JsonDocument.Parse(message);
			var root = jsonDoc.RootElement;

			if (!root.TryGetProperty("type", out var typeElement))
			{
				Console.WriteLine("Message has no 'type' property");
				return;
			}

			var messageType = typeElement.GetString();

			// Handle screenshot response
			if (messageType == MobileAppMessageTypes.ScreenshotResponse)
			{
				var response = JsonSerializer.Deserialize<ScreenshotResponseMessage>(message, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				if (response != null && _screenshotRequests.TryGetValue(response.DeviceId, out var tcs))
				{
					if (response.Success)
					{
						tcs.TrySetResult(response.ImageData);
					}
					else
					{
						Console.WriteLine($"Screenshot failed: {response.ErrorMessage}");
						tcs.TrySetResult(null);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error processing message: {ex.Message}");
		}
	}

	/// <summary>
	/// Disposes the WebSocket service.
	/// </summary>
	public void Dispose()
	{
		DisconnectAsync().Wait();
		GC.SuppressFinalize(this);
	}
}
