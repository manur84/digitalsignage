using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalSignage.Server.Views;
using Serilog;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Manages application startup with detailed progress tracking for splash screen
/// </summary>
public class StartupProgressManager
{
    private readonly SplashScreenWindow? _splashScreen;
    private readonly ILogger _logger;
    private readonly List<StartupStep> _steps;
    private int _currentStepIndex;

    public StartupProgressManager(SplashScreenWindow? splashScreen)
    {
        _splashScreen = splashScreen;
        _logger = Log.ForContext<StartupProgressManager>();
        _steps = new List<StartupStep>();
        _currentStepIndex = 0;
    }

    /// <summary>
    /// Define all startup steps with their weights
    /// </summary>
    public void DefineSteps(params StartupStep[] steps)
    {
        _steps.Clear();
        _steps.AddRange(steps);
        _currentStepIndex = 0;
    }

    /// <summary>
    /// Execute a startup step with progress tracking
    /// </summary>
    public async Task ExecuteStepAsync(Func<Task> action, string? customMessage = null)
    {
        if (_currentStepIndex >= _steps.Count)
        {
            _logger.Warning("ExecuteStepAsync called but no more steps defined");
            return;
        }

        var step = _steps[_currentStepIndex];
        var message = customMessage ?? step.Message;

        try
        {
            _logger.Information("Starting step {StepIndex}/{TotalSteps}: {Message}",
                _currentStepIndex + 1, _steps.Count, message);

            // Calculate progress based on completed steps
            var baseProgress = CalculateProgressUpToStep(_currentStepIndex);
            var stepWeight = step.Weight;

            // Update splash with step start
            _splashScreen?.UpdateProgress(baseProgress, message, step.DetailMessage);

            // Execute the step
            var startTime = DateTime.UtcNow;
            await action();
            var duration = DateTime.UtcNow - startTime;

            // Update progress to include completed step
            var completedProgress = CalculateProgressUpToStep(_currentStepIndex + 1);

            await _splashScreen?.AnimateProgressAsync(
                completedProgress,
                message,
                $"Abgeschlossen in {duration.TotalMilliseconds:F0}ms"
            )!;

            _logger.Information("Completed step {StepIndex}/{TotalSteps}: {Message} (Duration: {Duration}ms)",
                _currentStepIndex + 1, _steps.Count, message, duration.TotalMilliseconds);

            _currentStepIndex++;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to execute step {StepIndex}/{TotalSteps}: {Message}",
                _currentStepIndex + 1, _steps.Count, message);
            throw;
        }
    }

    /// <summary>
    /// Execute a synchronous startup step
    /// </summary>
    public async Task ExecuteStepAsync(Action action, string? customMessage = null)
    {
        await ExecuteStepAsync(() =>
        {
            action();
            return Task.CompletedTask;
        }, customMessage);
    }

    /// <summary>
    /// Calculate total progress percentage up to a specific step
    /// </summary>
    private double CalculateProgressUpToStep(int stepIndex)
    {
        if (_steps.Count == 0)
            return 0;

        var totalWeight = _steps.Sum(s => s.Weight);
        var completedWeight = _steps.Take(stepIndex).Sum(s => s.Weight);

        return (completedWeight / totalWeight) * 100.0;
    }

    /// <summary>
    /// Set progress to 100% and show completion message
    /// </summary>
    public async Task CompleteAsync()
    {
        _logger.Information("Startup completed successfully");
        await _splashScreen?.AnimateProgressAsync(100, "Gestartet!", "Öffne Hauptfenster...")!;
        await Task.Delay(500); // Brief pause to show completion
    }
}

/// <summary>
/// Represents a single startup step
/// </summary>
public class StartupStep
{
    public string Message { get; }
    public string DetailMessage { get; }
    public double Weight { get; }

    public StartupStep(string message, double weight = 1.0, string detailMessage = "")
    {
        Message = message;
        Weight = weight;
        DetailMessage = detailMessage;
    }
}
