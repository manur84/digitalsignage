using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for managing client registration tokens
/// </summary>
public partial class TokenManagementViewModel : ObservableObject
{
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<TokenManagementViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ClientRegistrationToken> _tokens = new();

    [ObservableProperty]
    private ClientRegistrationToken? _selectedToken;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Add Token Properties
    [ObservableProperty]
    private string _newTokenDescription = string.Empty;

    [ObservableProperty]
    private bool _newTokenHasExpiration;

    [ObservableProperty]
    private DateTime _newTokenExpirationDate = DateTime.Now.AddMonths(1);

    [ObservableProperty]
    private bool _newTokenHasMaxUses;

    [ObservableProperty]
    private int _newTokenMaxUses = 1;

    [ObservableProperty]
    private string _newTokenAllowedMacAddress = string.Empty;

    [ObservableProperty]
    private string _newTokenAllowedGroup = string.Empty;

    [ObservableProperty]
    private string _newTokenAllowedLocation = string.Empty;

    [ObservableProperty]
    private bool _isAddTokenDialogOpen;

    public TokenManagementViewModel(
        IDbContextFactory<DigitalSignageDbContext> contextFactory,
        IAuthenticationService authService,
        ILogger<TokenManagementViewModel> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load tokens on initialization
        _ = LoadTokensAsync();
    }

    /// <summary>
    /// Load all tokens from the database
    /// </summary>
    [RelayCommand]
    private async Task LoadTokensAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading tokens...";

        try
        {
            _logger.LogInformation("Loading client registration tokens from database");

            await using var context = await _contextFactory.CreateDbContextAsync();
            var tokens = await context.ClientRegistrationTokens
                .Include(t => t.CreatedByUser)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("Loaded {Count} tokens", tokens.Count);

            Tokens.Clear();
            foreach (var token in tokens)
            {
                Tokens.Add(token);
            }

            StatusMessage = $"Loaded {tokens.Count} token(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tokens");
            StatusMessage = $"Error loading tokens: {ex.Message}";
            MessageBox.Show($"Failed to load tokens: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Open the Add Token dialog
    /// </summary>
    [RelayCommand]
    private void OpenAddTokenDialog()
    {
        _logger.LogInformation("Opening Add Token dialog");

        // Reset fields
        NewTokenDescription = string.Empty;
        NewTokenHasExpiration = false;
        NewTokenExpirationDate = DateTime.Now.AddMonths(1);
        NewTokenHasMaxUses = true;
        NewTokenMaxUses = 1;
        NewTokenAllowedMacAddress = string.Empty;
        NewTokenAllowedGroup = string.Empty;
        NewTokenAllowedLocation = string.Empty;

        IsAddTokenDialogOpen = true;
    }

    /// <summary>
    /// Cancel adding a new token
    /// </summary>
    [RelayCommand]
    private void CancelAddToken()
    {
        _logger.LogInformation("Cancelled adding token");
        IsAddTokenDialogOpen = false;
    }

    /// <summary>
    /// Generate and add a new registration token
    /// </summary>
    [RelayCommand]
    private async Task AddTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTokenDescription))
        {
            MessageBox.Show("Please provide a description for the token.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        StatusMessage = "Creating new token...";

        try
        {
            _logger.LogInformation("Creating new registration token: {Description}", NewTokenDescription);

            // Single-user mode: All tokens are created by User ID 1 (Administrator)
            // Multi-user authentication is not currently implemented
            // When authentication is added, this should use the current logged-in user's ID
            int userId = 1;

            await using var context = await _contextFactory.CreateDbContextAsync();

            // Ensure default user exists
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                // Create default system user
                user = new User
                {
                    Username = "system",
                    Email = "system@digitalsignage.local",
                    PasswordHash = "not-set",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();
                _logger.LogInformation("Created default system user with ID {UserId}", user.Id);
                userId = user.Id;
            }

            // Create token using AuthenticationService
            var tokenString = await _authService.CreateRegistrationTokenAsync(
                createdByUserId: userId,
                description: NewTokenDescription,
                expiresAt: NewTokenHasExpiration ? NewTokenExpirationDate : null,
                maxUses: NewTokenHasMaxUses ? NewTokenMaxUses : null,
                allowedMacAddress: string.IsNullOrWhiteSpace(NewTokenAllowedMacAddress) ? null : NewTokenAllowedMacAddress,
                allowedGroup: string.IsNullOrWhiteSpace(NewTokenAllowedGroup) ? null : NewTokenAllowedGroup,
                allowedLocation: string.IsNullOrWhiteSpace(NewTokenAllowedLocation) ? null : NewTokenAllowedLocation);

            _logger.LogInformation("Successfully created token: {Token}", tokenString);

            // Reload tokens
            await LoadTokensAsync();

            // Close dialog
            IsAddTokenDialogOpen = false;

            // Show success message with token value
            var message = $"Token created successfully!\n\nToken: {tokenString}\n\nCopy this token now - it will not be shown again in plain text.";
            MessageBox.Show(message, "Token Created", MessageBoxButton.OK, MessageBoxImage.Information);

            // Auto-copy to clipboard
            try
            {
                Clipboard.SetText(tokenString);
                StatusMessage = "Token created and copied to clipboard";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy token to clipboard");
                StatusMessage = "Token created successfully";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create token");
            StatusMessage = $"Error creating token: {ex.Message}";
            MessageBox.Show($"Failed to create token: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Copy selected token to clipboard
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteTokenCommand))]
    private void CopyToken()
    {
        if (SelectedToken == null)
            return;

        try
        {
            Clipboard.SetText(SelectedToken.Token);
            _logger.LogInformation("Copied token {TokenId} to clipboard", SelectedToken.Id);
            StatusMessage = "Token copied to clipboard";
            MessageBox.Show("Token copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy token to clipboard");
            MessageBox.Show($"Failed to copy token: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Mark token as revoked (set IsUsed = true, even if not actually used)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteTokenCommand))]
    private async Task RevokeTokenAsync()
    {
        if (SelectedToken == null)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to revoke this token?\n\nDescription: {SelectedToken.Description}\n\nRevoked tokens cannot be used for new registrations.",
            "Confirm Revoke",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        IsLoading = true;
        StatusMessage = "Revoking token...";

        try
        {
            _logger.LogInformation("Revoking token {TokenId}", SelectedToken.Id);

            await using var context = await _contextFactory.CreateDbContextAsync();
            var token = await context.ClientRegistrationTokens.FindAsync(SelectedToken.Id);

            if (token != null)
            {
                token.IsUsed = true;
                if (!token.UsedAt.HasValue)
                {
                    token.UsedAt = DateTime.UtcNow;
                }
                await context.SaveChangesAsync();

                _logger.LogInformation("Successfully revoked token {TokenId}", SelectedToken.Id);
                StatusMessage = "Token revoked successfully";

                // Reload tokens
                await LoadTokensAsync();
            }
            else
            {
                _logger.LogWarning("Token {TokenId} not found for revocation", SelectedToken.Id);
                StatusMessage = "Token not found";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke token {TokenId}", SelectedToken?.Id);
            StatusMessage = $"Error revoking token: {ex.Message}";
            MessageBox.Show($"Failed to revoke token: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Delete a token from the database
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteTokenCommand))]
    private async Task DeleteTokenAsync()
    {
        if (SelectedToken == null)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to permanently delete this token?\n\nDescription: {SelectedToken.Description}\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        IsLoading = true;
        StatusMessage = "Deleting token...";

        try
        {
            _logger.LogInformation("Deleting token {TokenId}", SelectedToken.Id);

            await using var context = await _contextFactory.CreateDbContextAsync();
            var token = await context.ClientRegistrationTokens.FindAsync(SelectedToken.Id);

            if (token != null)
            {
                context.ClientRegistrationTokens.Remove(token);
                await context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted token {TokenId}", SelectedToken.Id);
                StatusMessage = "Token deleted successfully";

                // Reload tokens
                await LoadTokensAsync();
            }
            else
            {
                _logger.LogWarning("Token {TokenId} not found for deletion", SelectedToken.Id);
                StatusMessage = "Token not found";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete token {TokenId}", SelectedToken?.Id);
            StatusMessage = $"Error deleting token: {ex.Message}";
            MessageBox.Show($"Failed to delete token: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refresh the token list
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadTokensAsync();
    }

    /// <summary>
    /// Check if token commands can be executed
    /// </summary>
    private bool CanExecuteTokenCommand()
    {
        return SelectedToken != null && !IsLoading;
    }

    /// <summary>
    /// Update command states when selection changes
    /// </summary>
    partial void OnSelectedTokenChanged(ClientRegistrationToken? value)
    {
        CopyTokenCommand.NotifyCanExecuteChanged();
        RevokeTokenCommand.NotifyCanExecuteChanged();
        DeleteTokenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Update command states when loading changes
    /// </summary>
    partial void OnIsLoadingChanged(bool value)
    {
        CopyTokenCommand.NotifyCanExecuteChanged();
        RevokeTokenCommand.NotifyCanExecuteChanged();
        DeleteTokenCommand.NotifyCanExecuteChanged();
    }
}
