using Microsoft.Extensions.Logging;
using System.Windows;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing application themes (Light/Dark mode)
/// </summary>
public class ThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private const string ThemeSettingKey = "AppTheme";
    private string _currentTheme = "Light";

    public event EventHandler<string>? ThemeChanged;

    public string CurrentTheme => _currentTheme;

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        LoadTheme();
    }

    /// <summary>
    /// Loads the saved theme from settings or defaults to Light
    /// </summary>
    public void LoadTheme()
    {
        try
        {
            // Try to load theme from application settings
            var savedTheme = Properties.Settings.Default.AppTheme;
            if (!string.IsNullOrEmpty(savedTheme))
            {
                _currentTheme = savedTheme;
                _logger.LogInformation("Loaded theme from settings: {Theme}", _currentTheme);
            }
            else
            {
                _currentTheme = "Light";
                _logger.LogInformation("No saved theme found, using default: Light");
            }

            ApplyTheme(_currentTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading theme, using Light theme");
            _currentTheme = "Light";
            ApplyTheme("Light");
        }
    }

    /// <summary>
    /// Switches to a different theme
    /// </summary>
    public void SwitchTheme(string theme)
    {
        if (theme != "Light" && theme != "Dark")
        {
            _logger.LogWarning("Invalid theme: {Theme}, must be 'Light' or 'Dark'", theme);
            return;
        }

        if (_currentTheme == theme)
        {
            _logger.LogDebug("Theme is already set to: {Theme}", theme);
            return;
        }

        _currentTheme = theme;
        ApplyTheme(theme);
        SaveTheme(theme);

        ThemeChanged?.Invoke(this, theme);
        _logger.LogInformation("Switched to {Theme} theme", theme);
    }

    /// <summary>
    /// Toggles between Light and Dark theme
    /// </summary>
    public void ToggleTheme()
    {
        var newTheme = _currentTheme == "Light" ? "Dark" : "Light";
        SwitchTheme(newTheme);
    }

    /// <summary>
    /// Applies the theme to the application
    /// </summary>
    private void ApplyTheme(string theme)
    {
        try
        {
            var themeUri = new Uri($"/Themes/{theme}Theme.xaml", UriKind.Relative);
            var themeDictionary = new ResourceDictionary { Source = themeUri };

            // Get the merged dictionaries
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            // Remove old theme dictionaries
            var oldThemes = mergedDictionaries
                .Where(d => d.Source?.OriginalString?.Contains("/Themes/") == true)
                .ToList();

            foreach (var oldTheme in oldThemes)
            {
                mergedDictionaries.Remove(oldTheme);
            }

            // Add new theme dictionary at the beginning so it has priority
            mergedDictionaries.Insert(0, themeDictionary);

            _logger.LogDebug("Applied {Theme} theme resource dictionary", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply {Theme} theme", theme);
        }
    }

    /// <summary>
    /// Saves the theme preference to settings
    /// </summary>
    private void SaveTheme(string theme)
    {
        try
        {
            Properties.Settings.Default.AppTheme = theme;
            Properties.Settings.Default.Save();
            _logger.LogDebug("Saved theme preference: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save theme preference");
        }
    }

    /// <summary>
    /// Gets the available themes
    /// </summary>
    public static string[] GetAvailableThemes()
    {
        return new[] { "Light", "Dark" };
    }
}
