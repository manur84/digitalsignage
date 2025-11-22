using Microsoft.EntityFrameworkCore;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using Serilog;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing mobile app registrations and authorization
/// </summary>
public class MobileAppService : IMobileAppService
{
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public MobileAppService(
        DigitalSignageDbContext dbContext,
        IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _logger = Log.ForContext<MobileAppService>();
    }

    public async Task<Result<MobileAppRegistration>> RegisterAppAsync(
        string deviceName,
        string deviceIdentifier,
        string appVersion,
        string platform)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(deviceName))
                return Result<MobileAppRegistration>.Failure("Device name is required");

            if (string.IsNullOrWhiteSpace(deviceIdentifier))
                return Result<MobileAppRegistration>.Failure("Device identifier is required");

            if (string.IsNullOrWhiteSpace(appVersion))
                return Result<MobileAppRegistration>.Failure("App version is required");

            if (string.IsNullOrWhiteSpace(platform))
                return Result<MobileAppRegistration>.Failure("Platform is required");

            // Check if device is already registered
            var existing = await _dbContext.MobileAppRegistrations
                .FirstOrDefaultAsync(r => r.DeviceIdentifier == deviceIdentifier);

            if (existing != null)
            {
                // If already approved, return existing registration
                if (existing.Status == AppRegistrationStatus.Approved)
                {
                    _logger.Information(
                        "Device {DeviceIdentifier} already registered and approved",
                        deviceIdentifier);
                    return Result<MobileAppRegistration>.Success(existing);
                }

                // If rejected or revoked, update to pending again
                if (existing.Status == AppRegistrationStatus.Rejected ||
                    existing.Status == AppRegistrationStatus.Revoked)
                {
                    existing.Status = AppRegistrationStatus.Pending;
                    existing.RegisteredAt = DateTime.UtcNow;
                    existing.DeviceName = deviceName;
                    existing.AppVersion = appVersion;
                    existing.Platform = platform;
                    existing.AuthorizedAt = null;
                    existing.AuthorizedBy = null;

                    await _dbContext.SaveChangesAsync();

                    _logger.Information(
                        "Device {DeviceIdentifier} re-registered (was {OldStatus})",
                        deviceIdentifier,
                        existing.Status);

                    return Result<MobileAppRegistration>.Success(existing);
                }

                // If pending, just return it
                _logger.Information(
                    "Device {DeviceIdentifier} already has pending registration",
                    deviceIdentifier);
                return Result<MobileAppRegistration>.Success(existing);
            }

            // Create new registration
            var registration = new MobileAppRegistration
            {
                Id = Guid.NewGuid(),
                DeviceName = deviceName,
                DeviceIdentifier = deviceIdentifier,
                AppVersion = appVersion,
                Platform = platform,
                Status = AppRegistrationStatus.Pending,
                RegisteredAt = DateTime.UtcNow,
                Permissions = string.Empty
            };

            _dbContext.MobileAppRegistrations.Add(registration);
            await _dbContext.SaveChangesAsync();

            _logger.Information(
                "New mobile app registered: {DeviceName} ({DeviceIdentifier})",
                deviceName,
                deviceIdentifier);

            return Result<MobileAppRegistration>.Success(registration);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error registering mobile app");
            return Result<MobileAppRegistration>.Failure("Failed to register mobile app");
        }
    }

    public async Task<Result<string>> ApproveAppAsync(
        Guid appId,
        string authorizedBy,
        AppPermission permissions)
    {
        try
        {
            var registration = await _dbContext.MobileAppRegistrations
                .FirstOrDefaultAsync(r => r.Id == appId);

            if (registration == null)
                return Result<string>.Failure("Registration not found");

            if (registration.Status != AppRegistrationStatus.Pending)
                return Result<string>.Failure($"Cannot approve registration with status {registration.Status}");

            // Generate secure token
            var token = GenerateSecureToken();

            // Update registration
            registration.Status = AppRegistrationStatus.Approved;
            registration.Token = token;
            registration.Permissions = ConvertPermissionsToString(permissions);
            registration.AuthorizedAt = DateTime.UtcNow;
            registration.AuthorizedBy = authorizedBy;
            registration.LastSeenAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.Information(
                "Mobile app approved: {DeviceName} by {AuthorizedBy} with permissions {Permissions}",
                registration.DeviceName,
                authorizedBy,
                registration.Permissions);

            // CRITICAL FIX: Send approval notification to iOS App via WebSocket
            // This prevents the app from being stuck in "Waiting for approval" state
            try
            {
                // Get WebSocketCommunicationService from service provider
                var webSocketService = _serviceProvider.GetService<WebSocketCommunicationService>();

                if (webSocketService != null)
                {
                    _logger.Information("Sending approval notification to mobile app {AppId} via WebSocket", appId);

                    // Send approval notification to iOS App
                    // The WebSocketCommunicationService will find the connection by MobileAppId
                    await webSocketService.SendApprovalNotificationAsync(appId, token, permissions);

                    _logger.Information("Approval notification sent successfully to mobile app {AppId}", appId);
                }
                else
                {
                    _logger.Warning("WebSocketCommunicationService not available - mobile app will poll for approval status");
                }
            }
            catch (Exception wsEx)
            {
                // Don't fail approval if WebSocket notification fails
                // The app can still poll for status or reconnect to get approved state
                _logger.Warning(wsEx, "Failed to send WebSocket approval notification to mobile app {AppId} - app will need to poll or reconnect", appId);
            }

            return Result<string>.Success(token);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error approving mobile app registration {AppId}", appId);
            return Result<string>.Failure("Failed to approve registration");
        }
    }

    public async Task<Result> RejectAppAsync(Guid appId, string reason)
    {
        try
        {
            var registration = await _dbContext.MobileAppRegistrations
                .FirstOrDefaultAsync(r => r.Id == appId);

            if (registration == null)
                return Result.Failure("Registration not found");

            if (registration.Status != AppRegistrationStatus.Pending)
                return Result.Failure($"Cannot reject registration with status {registration.Status}");

            registration.Status = AppRegistrationStatus.Rejected;
            registration.Notes = reason;
            registration.AuthorizedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.Information(
                "Mobile app rejected: {DeviceName} - {Reason}",
                registration.DeviceName,
                reason);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error rejecting mobile app registration {AppId}", appId);
            return Result.Failure("Failed to reject registration");
        }
    }

    public async Task<Result> RevokeAppAsync(Guid appId, string reason)
    {
        try
        {
            var registration = await _dbContext.MobileAppRegistrations
                .FirstOrDefaultAsync(r => r.Id == appId);

            if (registration == null)
                return Result.Failure("Registration not found");

            if (registration.Status != AppRegistrationStatus.Approved)
                return Result.Failure($"Cannot revoke registration with status {registration.Status}");

            registration.Status = AppRegistrationStatus.Revoked;
            registration.Notes = reason;
            registration.Token = null; // Invalidate token

            await _dbContext.SaveChangesAsync();

            _logger.Warning(
                "Mobile app access revoked: {DeviceName} - {Reason}",
                registration.DeviceName,
                reason);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error revoking mobile app registration {AppId}", appId);
            return Result.Failure("Failed to revoke registration");
        }
    }

    public async Task<MobileAppRegistration?> ValidateTokenAsync(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var registration = await _dbContext.MobileAppRegistrations
                .FirstOrDefaultAsync(r => r.Token == token && r.Status == AppRegistrationStatus.Approved);

            return registration;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating token");
            return null;
        }
    }

    public async Task<Result<Guid>> ValidateTokenAsync2(string token)
    {
        try
        {
            var registration = await ValidateTokenAsync(token);
            if (registration == null)
                return Result<Guid>.Failure("Invalid or expired token");

            return Result<Guid>.Success(registration.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating token");
            return Result<Guid>.Failure("Token validation failed");
        }
    }

    public async Task<MobileAppRegistration?> GetRegistrationAsync(Guid appId)
    {
        try
        {
            return await _dbContext.MobileAppRegistrations
                .FirstOrDefaultAsync(r => r.Id == appId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting registration {AppId}", appId);
            return null;
        }
    }

    public async Task<MobileAppRegistration?> GetRegistrationByDeviceAsync(string deviceIdentifier)
    {
        try
        {
            return await _dbContext.MobileAppRegistrations
                .FirstOrDefaultAsync(r => r.DeviceIdentifier == deviceIdentifier);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting registration for device {DeviceIdentifier}", deviceIdentifier);
            return null;
        }
    }

    public async Task<List<MobileAppRegistration>> GetAllRegistrationsAsync(AppRegistrationStatus? status = null)
    {
        try
        {
            var query = _dbContext.MobileAppRegistrations.AsQueryable();

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            return await query
                .OrderByDescending(r => r.RegisteredAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting all registrations");
            return new List<MobileAppRegistration>();
        }
    }

    public async Task<int> GetPendingCountAsync()
    {
        try
        {
            return await _dbContext.MobileAppRegistrations
                .CountAsync(r => r.Status == AppRegistrationStatus.Pending);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting pending count");
            return 0;
        }
    }

    public async Task<Result> UpdateLastSeenAsync(string token)
    {
        try
        {
            var registration = await ValidateTokenAsync(token);
            if (registration == null)
                return Result.Failure("Invalid token");

            registration.LastSeenAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating last seen");
            return Result.Failure("Failed to update last seen");
        }
    }

    public async Task<bool> HasPermissionAsync(string token, AppPermission permission)
    {
        try
        {
            var registration = await ValidateTokenAsync(token);
            if (registration == null)
                return false;

            var permissions = ParsePermissions(registration.Permissions);
            return permissions.HasFlag(permission);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking permission");
            return false;
        }
    }

    public async Task<Result> DeleteRegistrationAsync(Guid appId)
    {
        try
        {
            var registration = await _dbContext.MobileAppRegistrations
                .FirstOrDefaultAsync(r => r.Id == appId);

            if (registration == null)
                return Result.Failure("Registration not found");

            _dbContext.MobileAppRegistrations.Remove(registration);
            await _dbContext.SaveChangesAsync();

            _logger.Information("Mobile app registration deleted: {DeviceName}", registration.DeviceName);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting registration {AppId}", appId);
            return Result.Failure("Failed to delete registration");
        }
    }

    public async Task<Result<Guid>> CreatePendingRegistrationAsync(
        string deviceName,
        string platform,
        string appVersion,
        string? deviceModel = null,
        string? osVersion = null)
    {
        try
        {
            // Generate unique device identifier
            var deviceIdentifier = $"{platform}_{deviceModel ?? "unknown"}_{Guid.NewGuid():N}";

            var result = await RegisterAppAsync(deviceName, deviceIdentifier, appVersion, platform);

            if (!result.IsSuccess)
                return Result<Guid>.Failure(result.ErrorMessage!);

            return Result<Guid>.Success(result.Value!.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating pending registration");
            return Result<Guid>.Failure("Failed to create registration");
        }
    }

    public async Task<Result<(string Status, string? Token, Guid? MobileAppId)>> GetRegistrationStatusAsync(Guid requestId)
    {
        try
        {
            var registration = await GetRegistrationAsync(requestId);

            if (registration == null)
                return Result<(string, string?, Guid?)>.Failure("Registration not found");

            var status = registration.Status.ToString();
            var token = registration.Status == AppRegistrationStatus.Approved ? registration.Token : null;
            var mobileAppId = registration.Status == AppRegistrationStatus.Approved ? (Guid?)registration.Id : null;

            return Result<(string, string?, Guid?)>.Success((status, token, mobileAppId));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting registration status");
            return Result<(string, string?, Guid?)>.Failure("Failed to get status");
        }
    }

    public async Task<Result<MobileAppRegistration>> GetMobileAppAsync(Guid appId)
    {
        try
        {
            var registration = await GetRegistrationAsync(appId);

            if (registration == null)
                return Result<MobileAppRegistration>.Failure("Mobile app not found");

            return Result<MobileAppRegistration>.Success(registration);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting mobile app");
            return Result<MobileAppRegistration>.Failure("Failed to get mobile app");
        }
    }

    // ============================================
    // PRIVATE HELPER METHODS
    // ============================================

    /// <summary>
    /// Generate a cryptographically secure token
    /// </summary>
    private static string GenerateSecureToken()
    {
        // Generate 32 bytes (256 bits) of random data
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        // Convert to Base64 (44 characters)
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")  // URL-safe
            .Replace("/", "_")  // URL-safe
            .TrimEnd('=');      // Remove padding
    }

    /// <summary>
    /// Convert AppPermission flags to comma-separated string
    /// </summary>
    private static string ConvertPermissionsToString(AppPermission permissions)
    {
        var parts = new List<string>();

        if (permissions.HasFlag(AppPermission.View))
            parts.Add("view");

        if (permissions.HasFlag(AppPermission.Control))
            parts.Add("control");

        if (permissions.HasFlag(AppPermission.Manage))
            parts.Add("manage");

        return string.Join(",", parts);
    }

    /// <summary>
    /// Parse comma-separated permission string to AppPermission flags
    /// </summary>
    private static AppPermission ParsePermissions(string permissionsString)
    {
        if (string.IsNullOrWhiteSpace(permissionsString))
            return AppPermission.None;

        var permissions = AppPermission.None;
        var parts = permissionsString.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var normalized = part.Trim().ToLowerInvariant();
            permissions |= normalized switch
            {
                "view" => AppPermission.View,
                "control" => AppPermission.Control,
                "manage" => AppPermission.Manage,
                _ => AppPermission.None
            };
        }

        return permissions;
    }
}
