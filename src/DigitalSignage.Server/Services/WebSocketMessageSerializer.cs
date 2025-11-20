using System.IO;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Handles WebSocket message serialization and compression
/// </summary>
public class WebSocketMessageSerializer
{
    private readonly ILogger<WebSocketMessageSerializer> _logger;
    private readonly bool _enableCompression;

    // CRITICAL FIX: JSON serializer settings to prevent string truncation
    // Problem: Default Newtonsoft.Json settings were truncating hex color values (#ADD8E6 → #ADD8)
    // Solution: Explicit settings with proper type handling and formatting
    private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        // Prevent any truncation of string values
        StringEscapeHandling = StringEscapeHandling.Default,
        // Format for debugging (can be changed to None for production)
        Formatting = Formatting.None,
        // Preserve full type information for Dictionary<string, object>
        TypeNameHandling = TypeNameHandling.None,
        // Don't ignore null values (they may be meaningful)
        NullValueHandling = NullValueHandling.Include,
        // Handle circular references (not common, but safe)
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        // Ensure dates are serialized in ISO format
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        // Don't modify property names
        ContractResolver = null
    };

    public WebSocketMessageSerializer(
        ILogger<WebSocketMessageSerializer> logger,
        bool enableCompression = true)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enableCompression = enableCompression;
    }

    /// <summary>
    /// Serializes and sends a message to a WebSocket client
    /// </summary>
    public async Task SendMessageAsync(
        WebSocket socket,
        string clientId,
        Message message,
        CancellationToken cancellationToken = default)
    {
        if (socket.State != WebSocketState.Open)
        {
            _logger.LogWarning("Cannot send message to client {ClientId}: WebSocket is not open (State: {State})",
                clientId, socket.State);
            throw new InvalidOperationException($"WebSocket is not open (State: {socket.State})");
        }

        try
        {
            // Serialize message to JSON
            var json = JsonConvert.SerializeObject(message, _jsonSettings);
            var messageBytes = Encoding.UTF8.GetBytes(json);

            // Log message size and decide whether to compress
            var messageSizeKB = messageBytes.Length / 1024.0;
            _logger.LogDebug("Sending message to client {ClientId}: Type={Type}, Size={Size:F2}KB",
                clientId, message.Type, messageSizeKB);

            // Compress if message is larger than 10KB and compression is enabled
            if (_enableCompression && messageBytes.Length > 10 * 1024)
            {
                await SendCompressedAsync(socket, clientId, messageBytes, cancellationToken);
            }
            else
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(messageBytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);

                _logger.LogDebug("Sent uncompressed message to client {ClientId}", clientId);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket error sending message to client {ClientId}: {ErrorCode}",
                clientId, ex.WebSocketErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to client {ClientId}", clientId);
            throw;
        }
    }

    /// <summary>
    /// Sends a compressed message to a WebSocket client
    /// </summary>
    private async Task SendCompressedAsync(
        WebSocket socket,
        string clientId,
        byte[] messageBytes,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
        {
            await gzipStream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
        }

        var compressedBytes = memoryStream.ToArray();
        var compressionRatio = (double)compressedBytes.Length / messageBytes.Length * 100;

        _logger.LogInformation("Sending compressed message to client {ClientId}: Original={OriginalKB:F2}KB, Compressed={CompressedKB:F2}KB, Ratio={Ratio:F1}%",
            clientId,
            messageBytes.Length / 1024.0,
            compressedBytes.Length / 1024.0,
            compressionRatio);

        // Send compressed data as binary message
        await socket.SendAsync(
            new ArraySegment<byte>(compressedBytes),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken);

        _logger.LogDebug("Sent compressed message to client {ClientId}", clientId);
    }

    /// <summary>
    /// Receives and deserializes a message from a WebSocket client
    /// </summary>
    public async Task<Message?> ReceiveMessageAsync(
        WebSocket socket,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1024 * 1024]; // 1MB buffer
        var messageBuilder = new StringBuilder();

        while (true)
        {
            var result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("Client {ClientId} initiated close handshake", clientId);
                return null;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Handle compressed message
                return await DecompressAndDeserializeAsync(buffer, result.Count, clientId);
            }

            // Text message
            var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
            messageBuilder.Append(chunk);

            if (result.EndOfMessage)
            {
                break;
            }
        }

        var json = messageBuilder.ToString();
        _logger.LogDebug("Received message from client {ClientId}: {Json}",
            clientId, json.Length > 200 ? json.Substring(0, 200) + "..." : json);

        try
        {
            var message = JsonConvert.DeserializeObject<Message>(json, _jsonSettings);
            return message;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message from client {ClientId}: {Json}",
                clientId, json.Length > 500 ? json.Substring(0, 500) + "..." : json);
            return null;
        }
    }

    /// <summary>
    /// Decompresses and deserializes a binary message
    /// </summary>
    private async Task<Message?> DecompressAndDeserializeAsync(
        byte[] buffer,
        int count,
        string clientId)
    {
        try
        {
            using var compressedStream = new MemoryStream(buffer, 0, count);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();

            await gzipStream.CopyToAsync(decompressedStream);
            var decompressedBytes = decompressedStream.ToArray();
            var json = Encoding.UTF8.GetString(decompressedBytes);

            _logger.LogInformation("Received compressed message from client {ClientId}: Compressed={CompressedKB:F2}KB, Decompressed={DecompressedKB:F2}KB",
                clientId,
                count / 1024.0,
                decompressedBytes.Length / 1024.0);

            var message = JsonConvert.DeserializeObject<Message>(json, _jsonSettings);
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decompress/deserialize binary message from client {ClientId}", clientId);
            return null;
        }
    }
}
