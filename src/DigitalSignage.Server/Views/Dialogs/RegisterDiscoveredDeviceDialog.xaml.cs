using System.Windows;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Views.Dialogs;

public partial class RegisterDiscoveredDeviceDialog : Window
{
    private readonly DiscoveredDevice _discoveredDevice;

    public RaspberryPiClient? RegisteredClient { get; private set; }

    public RegisterDiscoveredDeviceDialog(DiscoveredDevice discoveredDevice)
    {
        InitializeComponent();

        _discoveredDevice = discoveredDevice ?? throw new ArgumentNullException(nameof(discoveredDevice));

        // Populate discovered information
        DiscoveredHostname.Text = discoveredDevice.Hostname;
        DiscoveredIpAddress.Text = discoveredDevice.IpAddress;
        DiscoveredMacAddress.Text = discoveredDevice.MacAddress ?? "N/A";
        DiscoveryMethod.Text = discoveredDevice.DiscoveryMethod;

        // Pre-fill registration details with discovered information
        DeviceNameTextBox.Text = discoveredDevice.Hostname;
        DescriptionTextBox.Text = $"Discovered via {discoveredDevice.DiscoveryMethod} on {discoveredDevice.DiscoveredAt:g}";

        // Focus on name field
        DeviceNameTextBox.Focus();
        DeviceNameTextBox.SelectAll();
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(DeviceNameTextBox.Text))
        {
            MessageBox.Show("Please enter a device name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            DeviceNameTextBox.Focus();
            return;
        }

        try
        {
            // Create new client from discovered device
            RegisteredClient = new RaspberryPiClient
            {
                Id = Guid.NewGuid().ToString(),
                Name = DeviceNameTextBox.Text.Trim(),
                Group = GroupTextBox.Text.Trim(),
                Location = LocationTextBox.Text.Trim(),
                IpAddress = _discoveredDevice.IpAddress,
                MacAddress = _discoveredDevice.MacAddress ?? string.Empty,
                Status = ClientStatus.Offline,
                RegisteredAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["Description"] = DescriptionTextBox.Text.Trim(),
                    ["DiscoveryMethod"] = _discoveredDevice.DiscoveryMethod,
                    ["RegistrationToken"] = string.IsNullOrWhiteSpace(TokenTextBox.Text) ? string.Empty : TokenTextBox.Text.Trim()
                }
            };

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create client registration: {ex.Message}",
                "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
