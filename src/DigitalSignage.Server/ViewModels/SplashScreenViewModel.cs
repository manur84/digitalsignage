using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the SplashScreen with progress tracking and status messages
/// </summary>
public partial class SplashScreenViewModel : ObservableObject
{
    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusMessage = "Wird gestartet...";

    [ObservableProperty]
    private string _detailMessage = "";

    [ObservableProperty]
    private bool _isIndeterminate;

    /// <summary>
    /// Update progress with smooth increments
    /// </summary>
    public void UpdateProgress(double progress, string statusMessage, string detailMessage = "")
    {
        Progress = Math.Clamp(progress, 0, 100);
        StatusMessage = statusMessage;
        DetailMessage = detailMessage;
        IsIndeterminate = false;
    }

    /// <summary>
    /// Set indeterminate loading state
    /// </summary>
    public void SetIndeterminate(string statusMessage, string detailMessage = "")
    {
        IsIndeterminate = true;
        StatusMessage = statusMessage;
        DetailMessage = detailMessage;
    }

    /// <summary>
    /// Simulate smooth progress animation
    /// </summary>
    public async Task AnimateProgressAsync(double targetProgress, string statusMessage, string detailMessage = "", int durationMs = 300)
    {
        var startProgress = Progress;
        var steps = 20;
        var stepDelay = durationMs / steps;
        var progressStep = (targetProgress - startProgress) / steps;

        StatusMessage = statusMessage;
        DetailMessage = detailMessage;
        IsIndeterminate = false;

        for (int i = 0; i < steps; i++)
        {
            Progress = startProgress + (progressStep * (i + 1));
            await Task.Delay(stepDelay);
        }

        Progress = targetProgress;
    }
}
