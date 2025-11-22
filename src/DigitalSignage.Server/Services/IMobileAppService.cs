using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing mobile app registrations and authorization
/// </summary>
public interface IMobileAppService
{
    /// <summary>
    /// Register a new mobile app (creates pending registration)
    /// </summary>
    /// <param name="deviceName">Device name (e.g., "iPhone 15 Pro")</param>
    /// <param name="deviceIdentifier">Unique device identifier</param>
    /// <param name="appVersion">App version</param>
    /// <param name="platform">Platform (iOS, Android)</param>
    /// <returns>Registration with pending status</returns>
    Task<Result<MobileAppRegistration>> RegisterAppAsync(
        string deviceName,
        string deviceIdentifier,
        string appVersion,
        string platform);

    /// <summary>
    /// Approve a pending registration
    /// </summary>
    /// <param name="appId">Registration ID</param>
    /// <param name="authorizedBy">Admin username</param>
    /// <param name="permissions">Granted permissions</param>
    /// <returns>Authorization token</returns>
    Task<Result<string>> ApproveAppAsync(Guid appId, string authorizedBy, AppPermission permissions);

    /// <summary>
    /// Reject a pending registration
    /// </summary>
    /// <param name="appId">Registration ID</param>
    /// <param name="reason">Rejection reason</param>
    /// <returns>Success result</returns>
    Task<Result> RejectAppAsync(Guid appId, string reason);

    /// <summary>
    /// Revoke an approved registration
    /// </summary>
    /// <param name="appId">Registration ID</param>
    /// <param name="reason">Revocation reason</param>
    /// <returns>Success result</returns>
    Task<Result> RevokeAppAsync(Guid appId, string reason);

    /// <summary>
    /// Validate authentication token
    /// </summary>
    /// <param name="token">Token to validate</param>
    /// <returns>Registration if valid, null otherwise</returns>
    Task<MobileAppRegistration?> ValidateTokenAsync(string token);

    /// <summary>
    /// Get registration by ID
    /// </summary>
    /// <param name="appId">Registration ID</param>
    /// <returns>Registration or null</returns>
    Task<MobileAppRegistration?> GetRegistrationAsync(Guid appId);

    /// <summary>
    /// Get registration by device identifier
    /// </summary>
    /// <param name="deviceIdentifier">Device identifier</param>
    /// <returns>Registration or null</returns>
    Task<MobileAppRegistration?> GetRegistrationByDeviceAsync(string deviceIdentifier);

    /// <summary>
    /// Get all registrations with optional status filter
    /// </summary>
    /// <param name="status">Optional status filter</param>
    /// <returns>List of registrations</returns>
    Task<List<MobileAppRegistration>> GetAllRegistrationsAsync(AppRegistrationStatus? status = null);

    /// <summary>
    /// Get count of pending registrations
    /// </summary>
    /// <returns>Count of pending registrations</returns>
    Task<int> GetPendingCountAsync();

    /// <summary>
    /// Update last seen timestamp for app
    /// </summary>
    /// <param name="token">Authentication token</param>
    /// <returns>Success result</returns>
    Task<Result> UpdateLastSeenAsync(string token);

    /// <summary>
    /// Check if app has specific permission
    /// </summary>
    /// <param name="token">Authentication token</param>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if app has permission</returns>
    Task<bool> HasPermissionAsync(string token, AppPermission permission);

    /// <summary>
    /// Delete a registration
    /// </summary>
    /// <param name="appId">Registration ID</param>
    /// <returns>Success result</returns>
    Task<Result> DeleteRegistrationAsync(Guid appId);
}
