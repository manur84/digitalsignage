using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Provides async locks per key (e.g., MAC address, Token, Client ID)
/// Prevents race conditions when multiple operations target the same resource
/// </summary>
public class AsyncLockService : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed = false;

    /// <summary>
    /// Acquire a lock for the specified key and execute an action
    /// </summary>
    public async Task<T> ExecuteWithLockAsync<T>(
        string key,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (action == null)
            throw new ArgumentNullException(nameof(action));

        ThrowIfDisposed();

        // Get or create semaphore for this key
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        // Acquire lock
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            // Execute action while holding the lock
            return await action();
        }
        finally
        {
            // Always release lock
            semaphore.Release();

            // Cleanup: Remove semaphore if no one is waiting
            // This prevents memory leaks for one-time keys
            if (semaphore.CurrentCount == 1 && _locks.TryRemove(key, out _))
            {
                // Dispose only if successfully removed
                semaphore.Dispose();
            }
        }
    }

    /// <summary>
    /// Acquire a lock for the specified key and execute an action (non-generic overload)
    /// </summary>
    public async Task ExecuteWithLockAsync(
        string key,
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithLockAsync<object?>(key, async () =>
        {
            await action();
            return null;
        }, cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AsyncLockService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose all semaphores
        foreach (var semaphore in _locks.Values)
        {
            try
            {
                semaphore.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _locks.Clear();
    }
}
