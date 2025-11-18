using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Custom Serilog sink that writes logs to an ObservableCollection for real-time UI display.
/// Thread-safe and supports auto-scroll to latest logs.
/// </summary>
public class UISink : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;
    private readonly ObservableCollection<string> _logMessages;
    private readonly int _maxMessages;
    private readonly ConcurrentQueue<string> _pendingMessages = new();

    public UISink(
        ObservableCollection<string> logMessages,
        IFormatProvider? formatProvider = null,
        int maxMessages = 1000)
    {
        _logMessages = logMessages ?? throw new ArgumentNullException(nameof(logMessages));
        _formatProvider = formatProvider;
        _maxMessages = maxMessages;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        try
        {
            // Format the log message
            var message = logEvent.RenderMessage(_formatProvider);
            var timestamp = logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = GetLevelString(logEvent.Level);

            // Format: [timestamp] [LEVEL] message
            var formattedMessage = $"[{timestamp}] [{level}] {message}";

            // Add exception if present
            if (logEvent.Exception != null)
            {
                formattedMessage += $"\n    Exception: {logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}";
                if (!string.IsNullOrEmpty(logEvent.Exception.StackTrace))
                {
                    formattedMessage += $"\n    {logEvent.Exception.StackTrace}";
                }
            }

            // Queue for UI thread processing
            _pendingMessages.Enqueue(formattedMessage);

            // Dispatch to UI thread - check if already on UI thread first
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                System.Diagnostics.Debug.WriteLine("UISink: Dispatcher is null, cannot log message to UI");
                return;
            }

            Action processMessages = () =>
            {
                try
                {
                    // Process all pending messages
                    while (_pendingMessages.TryDequeue(out var msg))
                    {
                        _logMessages.Add(msg);

                        // Trim if exceeded max messages
                        while (_logMessages.Count > _maxMessages)
                        {
                            _logMessages.RemoveAt(0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Avoid recursion - log to debug output
                    System.Diagnostics.Debug.WriteLine($"UISink error: {ex.Message}");
                }
            };

            if (dispatcher.CheckAccess())
            {
                processMessages();
            }
            else
            {
                dispatcher.InvokeAsync(processMessages, System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        catch (Exception ex)
        {
            // Log to debug output to avoid recursion
            System.Diagnostics.Debug.WriteLine($"UISink emit error: {ex.Message}");
        }
    }

    private static string GetLevelString(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "VERB",
            LogEventLevel.Debug => "DEBG",
            LogEventLevel.Information => "INFO",
            LogEventLevel.Warning => "WARN",
            LogEventLevel.Error => "ERRO",
            LogEventLevel.Fatal => "FATL",
            _ => level.ToString().ToUpper()
        };
    }
}

/// <summary>
/// Extension methods for adding UISink to Serilog configuration
/// </summary>
public static class UISinkExtensions
{
    public static Serilog.LoggerConfiguration UISink(
        this Serilog.Configuration.LoggerSinkConfiguration sinkConfiguration,
        ObservableCollection<string> logMessages,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose,
        IFormatProvider? formatProvider = null,
        int maxMessages = 1000)
    {
        if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));

        return sinkConfiguration.Sink(
            new UISink(logMessages, formatProvider, maxMessages),
            restrictedToMinimumLevel);
    }
}
