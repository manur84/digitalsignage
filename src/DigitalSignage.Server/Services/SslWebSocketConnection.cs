using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// WebSocket connection over SSL/TLS using SslStream
/// Implements RFC 6455 WebSocket Protocol over TLS/SSL
/// </summary>
public class SslWebSocketConnection : IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly SslStream _sslStream;
    private readonly ILogger _logger;
    private bool _isHandshakeComplete;
    private bool _disposed;

    public string ConnectionId { get; }
    public string? ClientIpAddress { get; }
    public bool IsConnected => !_disposed && _tcpClient.Connected && _isHandshakeComplete;

    public SslWebSocketConnection(TcpClient tcpClient, SslStream sslStream, ILogger logger)
    {
        _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
        _sslStream = sslStream ?? throw new ArgumentNullException(nameof(sslStream));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConnectionId = Guid.NewGuid().ToString();
        ClientIpAddress = _tcpClient.Client.RemoteEndPoint?.ToString();
    }

    /// <summary>
    /// Perform WebSocket handshake over SSL/TLS connection (RFC 6455)
    /// </summary>
    public async Task<bool> PerformWebSocketHandshakeAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting WebSocket handshake for {ConnectionId}", ConnectionId);

            // 1. Read HTTP request
            var buffer = new byte[8192];
            var bytesRead = await _sslStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            if (bytesRead == 0)
            {
                _logger.LogWarning("No data received for WebSocket handshake from {ConnectionId}", ConnectionId);
                return false;
            }

            var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            _logger.LogDebug("WebSocket handshake request: {Request}", request.Length > 500 ? request.Substring(0, 500) : request);

            // 2. Parse Sec-WebSocket-Key
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            var keyLine = Array.Find(lines, l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase));
            if (keyLine == null)
            {
                _logger.LogWarning("Missing Sec-WebSocket-Key in handshake from {ConnectionId}", ConnectionId);
                return false;
            }

            var webSocketKey = keyLine.Split(':', 2)[1].Trim();

            // 3. Generate accept key (RFC 6455 Section 1.3)
            const string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            var acceptKey = Convert.ToBase64String(
                SHA1.HashData(Encoding.UTF8.GetBytes(webSocketKey + magicString))
            );

            // 4. Send handshake response
            var response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

            var responseBytes = Encoding.UTF8.GetBytes(response);
            await _sslStream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
            await _sslStream.FlushAsync(cancellationToken);

            _isHandshakeComplete = true;
            _logger.LogInformation("WebSocket handshake completed for {ConnectionId} from {ClientIp}", ConnectionId, ClientIpAddress);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket handshake failed for {ConnectionId}", ConnectionId);
            return false;
        }
    }

    /// <summary>
    /// Receive a WebSocket frame (RFC 6455 Section 5.2)
    /// </summary>
    public async Task<WebSocketFrame?> ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        if (!_isHandshakeComplete)
        {
            _logger.LogError("Cannot receive frame - handshake not complete for {ConnectionId}", ConnectionId);
            return null;
        }

        try
        {
            // Read frame header (minimum 2 bytes)
            var header = new byte[2];
            var bytesRead = await ReadExactAsync(_sslStream, header, 0, 2, cancellationToken);
            if (bytesRead != 2)
                return null;

            // Parse header
            bool fin = (header[0] & 0x80) != 0;
            byte opcode = (byte)(header[0] & 0x0F);
            bool masked = (header[1] & 0x80) != 0;
            int payloadLength = header[1] & 0x7F;

            // Read extended payload length if needed
            if (payloadLength == 126)
            {
                var extLength = new byte[2];
                await ReadExactAsync(_sslStream, extLength, 0, 2, cancellationToken);
                payloadLength = (extLength[0] << 8) | extLength[1];
            }
            else if (payloadLength == 127)
            {
                var extLength = new byte[8];
                await ReadExactAsync(_sslStream, extLength, 0, 8, cancellationToken);
                // For simplicity, we'll limit to int.MaxValue
                payloadLength = (int)((long)extLength[4] << 24 | (long)extLength[5] << 16 | (long)extLength[6] << 8 | extLength[7]);
            }

            // Read masking key if present (clients MUST mask their frames per RFC 6455)
            byte[]? maskingKey = null;
            if (masked)
            {
                maskingKey = new byte[4];
                await ReadExactAsync(_sslStream, maskingKey, 0, 4, cancellationToken);
            }

            // Read payload
            var payload = new byte[payloadLength];
            if (payloadLength > 0)
            {
                await ReadExactAsync(_sslStream, payload, 0, payloadLength, cancellationToken);

                // Unmask if needed
                if (masked && maskingKey != null)
                {
                    for (int i = 0; i < payloadLength; i++)
                    {
                        payload[i] ^= maskingKey[i % 4];
                    }
                }
            }

            return new WebSocketFrame
            {
                Fin = fin,
                Opcode = (WebSocketOpcode)opcode,
                Payload = payload
            };
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Connection closed while receiving WebSocket frame from {ConnectionId}", ConnectionId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving WebSocket frame from {ConnectionId}", ConnectionId);
            return null;
        }
    }

    /// <summary>
    /// Send a WebSocket frame (RFC 6455 Section 5.2)
    /// Server frames MUST NOT be masked per RFC 6455
    /// </summary>
    public async Task SendFrameAsync(WebSocketOpcode opcode, byte[] payload, CancellationToken cancellationToken)
    {
        if (!_isHandshakeComplete)
        {
            _logger.LogError("Cannot send frame - handshake not complete for {ConnectionId}", ConnectionId);
            return;
        }

        try
        {
            using var ms = new MemoryStream();

            // Frame header: FIN=1, RSV=0, opcode
            byte firstByte = (byte)(0x80 | (byte)opcode);
            ms.WriteByte(firstByte);

            // Payload length (server frames are NOT masked)
            if (payload.Length < 126)
            {
                ms.WriteByte((byte)payload.Length);
            }
            else if (payload.Length <= ushort.MaxValue)
            {
                ms.WriteByte(126);
                ms.WriteByte((byte)(payload.Length >> 8));
                ms.WriteByte((byte)(payload.Length & 0xFF));
            }
            else
            {
                ms.WriteByte(127);
                for (int i = 0; i < 8; i++)
                {
                    ms.WriteByte((byte)((payload.Length >> (8 * (7 - i))) & 0xFF));
                }
            }

            // Payload (no masking for server-to-client frames)
            if (payload.Length > 0)
            {
                ms.Write(payload, 0, payload.Length);
            }

            var frame = ms.ToArray();

            // CRITICAL DEBUG LOGGING: Log frame structure details
            _logger.LogDebug(
                "Sending WebSocket frame to {ConnectionId}: Opcode={Opcode}, PayloadLength={PayloadLength}, FrameSize={FrameSize}, MASK=0 (unmaskiert)",
                ConnectionId, opcode, payload.Length, frame.Length
            );

            await _sslStream.WriteAsync(frame, 0, frame.Length, cancellationToken);
            await _sslStream.FlushAsync(cancellationToken);

            _logger.LogDebug("Frame sent successfully to {ConnectionId}", ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WebSocket frame to {ConnectionId}", ConnectionId);
            throw;
        }
    }

    /// <summary>
    /// Send text message (UTF-8 encoded)
    /// </summary>
    public async Task SendTextAsync(string message, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        await SendFrameAsync(WebSocketOpcode.Text, payload, cancellationToken);
    }

    /// <summary>
    /// Send binary message
    /// </summary>
    public async Task SendBinaryAsync(byte[] data, CancellationToken cancellationToken)
    {
        await SendFrameAsync(WebSocketOpcode.Binary, data, cancellationToken);
    }

    /// <summary>
    /// Send close frame
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_isHandshakeComplete && !_disposed)
            {
                await SendFrameAsync(WebSocketOpcode.Close, Array.Empty<byte>(), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending close frame to {ConnectionId}", ConnectionId);
        }
    }

    /// <summary>
    /// Read exact number of bytes from stream
    /// </summary>
    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken);
            if (read == 0)
                break;
            totalRead += read;
        }
        return totalRead;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _sslStream?.Dispose();
            _tcpClient?.Close();
            _tcpClient?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing SslWebSocketConnection {ConnectionId}", ConnectionId);
        }
        finally
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// WebSocket frame structure (RFC 6455)
/// </summary>
public class WebSocketFrame
{
    public bool Fin { get; set; }
    public WebSocketOpcode Opcode { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// WebSocket opcodes (RFC 6455 Section 5.2)
/// </summary>
public enum WebSocketOpcode : byte
{
    Continuation = 0x0,
    Text = 0x1,
    Binary = 0x2,
    Close = 0x8,
    Ping = 0x9,
    Pong = 0xA
}
