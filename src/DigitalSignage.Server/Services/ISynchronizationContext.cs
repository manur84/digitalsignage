using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Abstraction for marshaling work to the UI thread (WPF Dispatcher).
/// </summary>
public interface ISynchronizationContext
{
    bool IsOnUiThread { get; }

    Task RunOnUiThreadAsync(Action action, CancellationToken cancellationToken = default);
    Task RunOnUiThreadAsync(Func<Task> action, CancellationToken cancellationToken = default);
}

/// <summary>
/// WPF implementation that uses Dispatcher to marshal actions to the UI thread.
/// </summary>
public sealed class WpfSynchronizationContextService : ISynchronizationContext
{
    private readonly Dispatcher _dispatcher;

    public WpfSynchronizationContextService()
    {
        _dispatcher = System.Windows.Application.Current?.Dispatcher
                   ?? Dispatcher.CurrentDispatcher;
    }

    public bool IsOnUiThread => _dispatcher.CheckAccess();

    public Task RunOnUiThreadAsync(Action action, CancellationToken cancellationToken = default)
    {
        if (IsOnUiThread)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>();
        _ = _dispatcher.BeginInvoke(new Action(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                action();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }));
        return tcs.Task;
    }

    public Task RunOnUiThreadAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (IsOnUiThread)
        {
            return action();
        }

        var tcs = new TaskCompletionSource<object?>();
        _ = _dispatcher.BeginInvoke(new Action(async () =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                await action().ConfigureAwait(true);
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }));
        return tcs.Task;
    }
}
