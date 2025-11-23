using DigitalSignage.App.Mobile.ViewModels;

namespace DigitalSignage.App.Mobile.Views;

/// <summary>
/// Settings page for app preferences and account management
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadSettingsAsync();
    }

    private async void OnDarkModeToggled(object sender, ToggledEventArgs e)
    {
        await _viewModel.ToggleDarkModeCommand.ExecuteAsync(null);
    }

    private async void OnBiometricAuthToggled(object sender, ToggledEventArgs e)
    {
        await _viewModel.ToggleBiometricAuthCommand.ExecuteAsync(null);
    }

    private async void OnPushNotificationsToggled(object sender, ToggledEventArgs e)
    {
        await _viewModel.TogglePushNotificationsCommand.ExecuteAsync(null);
    }

    private async void OnAutoConnectToggled(object sender, ToggledEventArgs e)
    {
        await _viewModel.ToggleAutoConnectCommand.ExecuteAsync(null);
    }
}
