using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services.FileStorage;

/// <summary>
/// File-based storage service for user management
/// </summary>
public class UserFileService : FileStorageService<UserInfo>
{
    private readonly ConcurrentDictionary<Guid, UserInfo> _usersCache = new();
    private readonly ConcurrentDictionary<string, Guid> _usernameToIdMap = new();
    private const string USERS_FILE = "users.json";

    public UserFileService(ILogger<UserFileService> logger) : base(logger)
    {
        _ = Task.Run(async () => await LoadUsersAsync());
    }

    protected override string GetSubDirectory() => "Settings";

    /// <summary>
    /// Load users into cache
    /// </summary>
    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await LoadListFromFileAsync(USERS_FILE);
            _usersCache.Clear();
            _usernameToIdMap.Clear();

            foreach (var user in users)
            {
                _usersCache[user.Id] = user;
                _usernameToIdMap[user.Username.ToLowerInvariant()] = user.Id;
            }

            // Create default admin if no users exist
            if (!users.Any())
            {
                await CreateDefaultAdminAsync();
            }

            _logger.LogInformation("Loaded {Count} users into cache", _usersCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users");
            // Create default admin on error
            await CreateDefaultAdminAsync();
        }
    }

    /// <summary>
    /// Create default admin user
    /// </summary>
    private async Task CreateDefaultAdminAsync()
    {
        var admin = new UserInfo
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@digitalsignage.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", workFactor: 12),
            Role = UserRole.Administrator,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLogin = null
        };

        _usersCache[admin.Id] = admin;
        _usernameToIdMap[admin.Username.ToLowerInvariant()] = admin.Id;

        await SaveUsersAsync();
        _logger.LogWarning("Created default admin user (username: admin, password: admin123) - PLEASE CHANGE THIS!");
    }

    /// <summary>
    /// Save users to file
    /// </summary>
    private async Task SaveUsersAsync()
    {
        try
        {
            var users = _usersCache.Values.ToList();
            await SaveListToFileAsync(USERS_FILE, users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save users");
        }
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    public async Task<UserInfo?> CreateUserAsync(string username, string email, string password, UserRole role = UserRole.User)
    {
        try
        {
            // Check if username already exists
            if (_usernameToIdMap.ContainsKey(username.ToLowerInvariant()))
            {
                _logger.LogWarning("Username {Username} already exists", username);
                return null;
            }

            // Check if email already exists
            if (_usersCache.Values.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Email {Email} already exists", email);
                return null;
            }

            var user = new UserInfo
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLogin = null
            };

            _usersCache[user.Id] = user;
            _usernameToIdMap[username.ToLowerInvariant()] = user.Id;

            await SaveUsersAsync();
            _logger.LogInformation("Created user {Username} with role {Role}", username, role);

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user {Username}", username);
            return null;
        }
    }

    /// <summary>
    /// Authenticate a user
    /// </summary>
    public async Task<UserInfo?> AuthenticateAsync(string username, string password)
    {
        try
        {
            if (_usernameToIdMap.TryGetValue(username.ToLowerInvariant(), out var userId) &&
                _usersCache.TryGetValue(userId, out var user))
            {
                if (!user.IsActive)
                {
                    _logger.LogWarning("Inactive user {Username} attempted to login", username);
                    return null;
                }

                if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    user.LastLogin = DateTime.UtcNow;
                    await SaveUsersAsync();
                    _logger.LogInformation("User {Username} authenticated successfully", username);
                    return user;
                }
            }

            _logger.LogWarning("Failed authentication attempt for {Username}", username);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating user {Username}", username);
            return null;
        }
    }

    /// <summary>
    /// Get all users
    /// </summary>
    public Task<List<UserInfo>> GetAllUsersAsync()
    {
        return Task.FromResult(_usersCache.Values.OrderBy(u => u.Username).ToList());
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    public Task<UserInfo?> GetUserByIdAsync(Guid userId)
    {
        _usersCache.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    /// <summary>
    /// Get user by username
    /// </summary>
    public Task<UserInfo?> GetUserByUsernameAsync(string username)
    {
        if (_usernameToIdMap.TryGetValue(username.ToLowerInvariant(), out var userId))
        {
            _usersCache.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }
        return Task.FromResult<UserInfo?>(null);
    }

    /// <summary>
    /// Update user password
    /// </summary>
    public async Task<bool> UpdatePasswordAsync(Guid userId, string newPassword)
    {
        try
        {
            if (_usersCache.TryGetValue(userId, out var user))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
                user.PasswordChangedAt = DateTime.UtcNow;

                await SaveUsersAsync();
                _logger.LogInformation("Updated password for user {Username}", user.Username);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update password for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Update user role
    /// </summary>
    public async Task<bool> UpdateUserRoleAsync(Guid userId, UserRole newRole)
    {
        try
        {
            if (_usersCache.TryGetValue(userId, out var user))
            {
                var oldRole = user.Role;
                user.Role = newRole;

                await SaveUsersAsync();
                _logger.LogInformation("Updated role for user {Username} from {OldRole} to {NewRole}",
                    user.Username, oldRole, newRole);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update role for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Activate or deactivate a user
    /// </summary>
    public async Task<bool> SetUserActiveStatusAsync(Guid userId, bool isActive)
    {
        try
        {
            if (_usersCache.TryGetValue(userId, out var user))
            {
                user.IsActive = isActive;

                await SaveUsersAsync();
                _logger.LogInformation("{Action} user {Username}",
                    isActive ? "Activated" : "Deactivated", user.Username);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update active status for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Delete a user
    /// </summary>
    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        try
        {
            if (_usersCache.TryRemove(userId, out var user))
            {
                _usernameToIdMap.TryRemove(user.Username.ToLowerInvariant(), out _);

                await SaveUsersAsync();
                _logger.LogInformation("Deleted user {Username}", user.Username);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Verify if a password meets requirements
    /// </summary>
    public bool IsPasswordValid(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        // Minimum 8 characters
        if (password.Length < 8)
            return false;

        // Must contain at least one uppercase, one lowercase, and one number
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasNumber = password.Any(char.IsDigit);

        return hasUpper && hasLower && hasNumber;
    }
}

/// <summary>
/// User information
/// </summary>
public class UserInfo
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// User roles
/// </summary>
public enum UserRole
{
    User,
    Editor,
    Administrator
}