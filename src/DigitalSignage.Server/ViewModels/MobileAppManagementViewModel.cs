using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for managing mobile app registrations
/// </summary>
public partial class MobileAppManagementViewModel : ObservableObject
{
    private readonly IMobileAppService _mobileAppService;
    private readonly ILogger<MobileAppManagementViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<MobileAppRegistration> _registrations = new();

    [ObservableProperty]
    private MobileAppRegistration? _selectedRegistration;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private bool _isLoading;

    // Permission checkboxes
    [ObservableProperty]
    private bool _viewPermission = true;

    [ObservableProperty]
    private bool _controlPermission = true;

    [ObservableProperty]
    private bool _managePermission = false;

    public MobileAppManagementViewModel(
        IMobileAppService mobileAppService,
        ILogger<MobileAppManagementViewModel> logger)
    {
        _mobileAppService = mobileAppService ?? throw new ArgumentNullException(nameof(mobileAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Load all registrations from database
    /// </summary>
    public async Task LoadRegistrationsAsync()
    {
        IsLoading = true;
        try
        {
            var result = await _mobileAppService.GetAllRegistrationsAsync();

            Registrations.Clear();
            foreach (var reg in result)
            {
                Registrations.Add(reg);
            }

            PendingCount = Registrations.Count(r => r.Status == AppRegistrationStatus.Pending);

            _logger.LogInformation("Loaded {Count} mobile app registrations ({Pending} pending)",
                Registrations.Count, PendingCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading mobile app registrations");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Approve the selected registration
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApprove))]
    private async Task ApproveAsync()
    {
        if (SelectedRegistration == null) return;

        try
        {
            var permissions = BuildPermissions();
            var result = await _mobileAppService.ApproveAppAsync(
                SelectedRegistration.Id,
                "Admin", // TODO(#2): Get actual admin username from authentication context
                permissions);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Approved mobile app: {DeviceName} with permissions {Permissions}",
                    SelectedRegistration.DeviceName, permissions);

                // Reload registrations
                await LoadRegistrationsAsync();

                // TODO(#3): Show success notification in UI
            }
            else
            {
                _logger.LogError("Failed to approve mobile app: {Error}", result.ErrorMessage);
                // TODO(#3): Show error notification in UI
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving mobile app registration");
        }
    }

    private bool CanApprove()
    {
        return SelectedRegistration != null &&
               SelectedRegistration.Status == AppRegistrationStatus.Pending;
    }

    /// <summary>
    /// Reject the selected registration
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReject))]
    private async Task RejectAsync()
    {
        if (SelectedRegistration == null) return;

        try
        {
            var result = await _mobileAppService.RejectAppAsync(
                SelectedRegistration.Id,
                "Rejected by admin");

            if (result.IsSuccess)
            {
                _logger.LogInformation("Rejected mobile app: {DeviceName}", SelectedRegistration.DeviceName);

                // Reload registrations
                await LoadRegistrationsAsync();

                // TODO(#3): Show success notification in UI
            }
            else
            {
                _logger.LogError("Failed to reject mobile app: {Error}", result.ErrorMessage);
                // TODO(#3): Show error notification in UI
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting mobile app registration");
        }
    }

    private bool CanReject()
    {
        return SelectedRegistration != null &&
               SelectedRegistration.Status == AppRegistrationStatus.Pending;
    }

    /// <summary>
    /// Revoke the selected registration
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRevoke))]
    private async Task RevokeAsync()
    {
        if (SelectedRegistration == null) return;

        try
        {
            var result = await _mobileAppService.RevokeAppAsync(
                SelectedRegistration.Id,
                "Revoked by admin");

            if (result.IsSuccess)
            {
                _logger.LogInformation("Revoked mobile app: {DeviceName}", SelectedRegistration.DeviceName);

                // Reload registrations
                await LoadRegistrationsAsync();

                // TODO(#3): Show success notification in UI
            }
            else
            {
                _logger.LogError("Failed to revoke mobile app: {Error}", result.ErrorMessage);
                // TODO(#3): Show error notification in UI
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking mobile app registration");
        }
    }

    private bool CanRevoke()
    {
        return SelectedRegistration != null &&
               SelectedRegistration.Status == AppRegistrationStatus.Approved;
    }

    /// <summary>
    /// Delete the selected registration
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (SelectedRegistration == null) return;

        try
        {
            var result = await _mobileAppService.DeleteRegistrationAsync(SelectedRegistration.Id);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Deleted mobile app registration: {DeviceName}", SelectedRegistration.DeviceName);

                // Reload registrations
                await LoadRegistrationsAsync();

                // TODO(#3): Show success notification in UI
            }
            else
            {
                _logger.LogError("Failed to delete mobile app: {Error}", result.ErrorMessage);
                // TODO(#3): Show error notification in UI
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting mobile app registration");
        }
    }

    private bool CanDelete()
    {
        return SelectedRegistration != null &&
               (SelectedRegistration.Status == AppRegistrationStatus.Rejected ||
                SelectedRegistration.Status == AppRegistrationStatus.Revoked);
    }

    /// <summary>
    /// Build permissions from checkboxes
    /// </summary>
    private AppPermission BuildPermissions()
    {
        var permissions = AppPermission.None;

        if (ViewPermission)
            permissions |= AppPermission.View;

        if (ControlPermission)
            permissions |= AppPermission.Control;

        if (ManagePermission)
            permissions |= AppPermission.Manage;

        return permissions;
    }

    /// <summary>
    /// Update permission checkboxes when selection changes
    /// </summary>
    partial void OnSelectedRegistrationChanged(MobileAppRegistration? value)
    {
        if (value != null && value.Status == AppRegistrationStatus.Approved)
        {
            // Parse existing permissions
            var permissions = ParsePermissions(value.Permissions);
            ViewPermission = permissions.HasFlag(AppPermission.View);
            ControlPermission = permissions.HasFlag(AppPermission.Control);
            ManagePermission = permissions.HasFlag(AppPermission.Manage);
        }
        else
        {
            // Default permissions for new approval
            ViewPermission = true;
            ControlPermission = true;
            ManagePermission = false;
        }

        // Update command can-execute states
        ApproveCommand.NotifyCanExecuteChanged();
        RejectCommand.NotifyCanExecuteChanged();
        RevokeCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Parse permissions string to AppPermission flags
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

    /// <summary>
    /// Handle new registration notification
    /// </summary>
    public async Task OnNewRegistrationAsync(MobileAppRegistration registration)
    {
        // Reload to get the new registration
        await LoadRegistrationsAsync();

        _logger.LogInformation("New mobile app registration received: {DeviceName}", registration.DeviceName);

        // TODO(#4): Show toast notification to user when new app registration is received
    }
}
