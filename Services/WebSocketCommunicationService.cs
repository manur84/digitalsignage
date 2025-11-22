private async Task<bool> SafeSendAsync(string clientId, string message)
{
    if (!_clients.TryGetValue(clientId, out var connection))
        return false;

    try
    {
        // Beispiel: send und explicit auf errors prüfen
        await connection.WebSocket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);

        return true;
    }
    catch (WebSocketException wex)
    {
        _logger.LogWarning("Send failed for {ClientId} (connId={ConnId}): {Message}", clientId, connection.Id, wex.Message);
        // Verbindung sauber schließen / aus Dictionary entfernen
        await TryCloseAndRemoveConnectionAsync(connection);
        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error while sending to {ClientId}", clientId);
        await TryCloseAndRemoveConnectionAsync(connection);
        return false;
    }
}