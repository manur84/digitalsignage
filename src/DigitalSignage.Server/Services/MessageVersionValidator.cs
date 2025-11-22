using DigitalSignage.Server.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Validates message protocol versions for backward compatibility
/// </summary>
public class MessageVersionValidator
{
    private readonly ILogger<MessageVersionValidator> _logger;

    // Cache client versions to avoid logging same warning multiple times
    private readonly ConcurrentDictionary<string, MessageVersion> _clientVersions = new();

    public MessageVersionValidator(ILogger<MessageVersionValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validate client version against server version
    /// </summary>
    public VersionCompatibilityResult ValidateVersion(string? versionString, string connectionId)
    {
        var serverVersion = MessageVersion.Current;

        // If no version provided, assume legacy client (pre-versioning)
        if (string.IsNullOrWhiteSpace(versionString))
        {
            _logger.LogWarning("Client {ConnectionId} did not provide protocol version. Assuming legacy client v1.0.0", connectionId);

            // Assume minimum version for legacy clients
            var legacyVersion = MessageVersion.Minimum;
            _clientVersions[connectionId] = legacyVersion;

            return VersionCompatibilityResult.Compatible(legacyVersion, serverVersion);
        }

        // Parse client version
        if (!MessageVersion.TryParse(versionString, out var clientVersion) || clientVersion == null)
        {
            _logger.LogError("Client {ConnectionId} provided invalid version string: {Version}", connectionId, versionString);

            return VersionCompatibilityResult.Incompatible(
                new MessageVersion(0, 0, 0),
                serverVersion,
                $"Invalid version format: {versionString}");
        }

        // Cache client version
        _clientVersions[connectionId] = clientVersion;

        // Check if version is supported
        if (!clientVersion.IsSupported())
        {
            _logger.LogError("Client {ConnectionId} version {ClientVersion} is not supported. " +
                "Minimum: {MinVersion}, Current: {CurrentVersion}",
                connectionId, clientVersion, MessageVersion.Minimum, serverVersion);

            return VersionCompatibilityResult.Incompatible(
                clientVersion,
                serverVersion,
                $"Version {clientVersion} is not supported. Please upgrade client.");
        }

        // Check compatibility
        if (!serverVersion.IsCompatibleWith(clientVersion))
        {
            _logger.LogError("Client {ConnectionId} version {ClientVersion} is incompatible with server version {ServerVersion}",
                connectionId, clientVersion, serverVersion);

            return VersionCompatibilityResult.Incompatible(
                clientVersion,
                serverVersion,
                $"Major version mismatch. Server requires v{serverVersion.Major}.x.x");
        }

        // Check if client is older than server (minor version difference)
        if (clientVersion.Minor < serverVersion.Minor)
        {
            _logger.LogInformation("Client {ConnectionId} is using older protocol version {ClientVersion} (server: {ServerVersion}). " +
                "Client should upgrade for new features.",
                connectionId, clientVersion, serverVersion);
        }

        // Check if client is newer than server (unusual but possible during deployment)
        if (clientVersion.Minor > serverVersion.Minor)
        {
            _logger.LogWarning("Client {ConnectionId} is using newer protocol version {ClientVersion} than server {ServerVersion}. " +
                "Server should be upgraded.",
                connectionId, clientVersion, serverVersion);
        }

        _logger.LogDebug("Client {ConnectionId} version {ClientVersion} is compatible with server {ServerVersion}",
            connectionId, clientVersion, serverVersion);

        return VersionCompatibilityResult.Compatible(clientVersion, serverVersion);
    }

    /// <summary>
    /// Get cached client version
    /// </summary>
    public MessageVersion? GetClientVersion(string connectionId)
    {
        return _clientVersions.TryGetValue(connectionId, out var version) ? version : null;
    }

    /// <summary>
    /// Remove client version from cache (on disconnect)
    /// </summary>
    public void RemoveClientVersion(string connectionId)
    {
        _clientVersions.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Get current server version
    /// </summary>
    public MessageVersion GetServerVersion() => MessageVersion.Current;

    /// <summary>
    /// Get minimum supported version
    /// </summary>
    public MessageVersion GetMinimumVersion() => MessageVersion.Minimum;

    /// <summary>
    /// Check if a specific feature is supported by client version
    /// </summary>
    public bool SupportsFeature(string connectionId, int requiredMajor, int requiredMinor)
    {
        var clientVersion = GetClientVersion(connectionId);
        if (clientVersion == null)
            return false;

        return clientVersion.Major > requiredMajor ||
               (clientVersion.Major == requiredMajor && clientVersion.Minor >= requiredMinor);
    }
}

/// <summary>
/// Extension methods for message versioning
/// </summary>
public static class MessageVersionExtensions
{
    /// <summary>
    /// Add version field to message JSON
    /// </summary>
    public static string WithVersion(this string messageJson)
    {
        // Simple approach: inject version at root level
        // More sophisticated: use JSON manipulation
        if (string.IsNullOrWhiteSpace(messageJson))
            return messageJson;

        // If message already has version, don't modify
        if (messageJson.Contains("\"version\"", StringComparison.OrdinalIgnoreCase))
            return messageJson;

        // Inject version after opening brace
        var version = MessageVersion.Current.ToString();
        return messageJson.Insert(1, $"\"version\":\"{version}\",");
    }
}
