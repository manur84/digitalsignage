# 🔧 Bug Fix TODO List - Actionable Tasks
## Digital Signage Projekt - Priorisierte Aufgabenliste

**Status:** 🔴 **0 von 67 Issues behoben**
**Letzte Aktualisierung:** 2025-11-15

---

## 📌 PHASE 1: KRITISCHE STABILITÄT (PRIORITÄT 1) - 16h

### ✅ Task 1.1: LayoutService - SemaphoreSlim Disposal
**Datei:** `src/DigitalSignage.Server/Services/LayoutService.cs`
**Priorität:** 🔴 CRITICAL
**Aufwand:** 30min
**Status:** ❌ TODO

**Änderungen:**
```csharp
// 1. IDisposable Interface hinzufügen
public class LayoutService : ILayoutService, IDisposable
{
    private bool _disposed = false;

    // 2. Dispose-Methode implementieren
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _fileLock?.Dispose();
            _logger.LogInformation("LayoutService disposed");
        }

        _disposed = true;
    }

    // 3. ThrowIfDisposed() zu allen public methods hinzufügen
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LayoutService));
    }

    public Task<List<DisplayLayout>> GetAllLayoutsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult(_layouts.Values.ToList());
    }
}
```

**Testplan:**
- [ ] Service-Lifetime testen
- [ ] Dispose zweimal aufrufen → keine Exception
- [ ] Nach Dispose Methode aufrufen → ObjectDisposedException

---

### ✅ Task 1.2: ClientService - SemaphoreSlim Disposal
**Datei:** `src/DigitalSignage.Server/Services/ClientService.cs`
**Priorität:** 🔴 CRITICAL
**Aufwand:** 30min
**Status:** ❌ TODO

**Änderungen:**
```csharp
public class ClientService : IClientService, IDisposable
{
    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _initSemaphore?.Dispose();
            _logger.LogInformation("ClientService disposed");
        }

        _disposed = true;
    }
}
```

**Testplan:**
- [ ] Service mit DI Container testen
- [ ] Scope-Disposal testen

---

### ✅ Task 1.3: WebSocketCommunicationService - HttpListener Complete Disposal
**Datei:** `src/DigitalSignage.Server/Services/WebSocketCommunicationService.cs`
**Priorität:** 🔴 CRITICAL
**Aufwand:** 1h
**Status:** ❌ TODO

**Änderungen:**
```csharp
public async Task StopAsync(CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Stopping WebSocket communication service...");

    // 1. Cancel accept loop
    _cancellationTokenSource?.Cancel();

    // 2. Stop listener
    if (_httpListener != null)
    {
        _httpListener.Stop();
        _httpListener.Close();
        _logger.LogDebug("HttpListener stopped and closed");
    }

    // 3. Close all client connections gracefully
    var closeTasks = _clients.Values.Select(async client =>
    {
        try
        {
            if (client.State == WebSocketState.Open)
            {
                await client.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server shutting down",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing client WebSocket during shutdown");
        }
    });

    await Task.WhenAll(closeTasks);

    // 4. Clear clients
    _clients.Clear();

    // 5. Dispose cancellation token source
    _cancellationTokenSource?.Dispose();
    _cancellationTokenSource = null;

    _logger.LogInformation("WebSocket communication service stopped");
}
```

**Testplan:**
- [ ] Server Start → Stop → Start wieder möglich
- [ ] Port-Blocking prüfen nach Stop
- [ ] Graceful shutdown mit verbundenen Clients testen

---

### ✅ Task 1.4: AlertsViewModel - Fire-and-Forget Constructor Fix
**Datei:** `src/DigitalSignage.Server/ViewModels/AlertsViewModel.cs`
**Priorität:** 🔴 CRITICAL
**Aufwand:** 2h
**Status:** ❌ TODO

**Änderungen:**
```csharp
private Task? _initializationTask;

public AlertsViewModel(...)
{
    _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
    // ... other assignments ...

    // Start initialization (tracked)
    _initializationTask = InitializeAsync();
}

private async Task InitializeAsync()
{
    try
    {
        await LoadDataAsync();
        StartPolling();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to initialize AlertsViewModel");

        // Try to show error in UI
        try
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _dialogService.ShowErrorAsync(
                    $"Failed to load alerts: {ex.Message}",
                    "Initialization Error");
            });
        }
        catch
        {
            // If UI is not available, just log
            _logger.LogError("Failed to show initialization error in UI");
        }
    }
}

// Public method to ensure initialization completed
public async Task EnsureInitializedAsync()
{
    if (_initializationTask != null)
    {
        await _initializationTask;
    }
}
```

**Testplan:**
- [ ] ViewModel erstellen → Alerts werden geladen
- [ ] DB-Fehler simulieren → Fehlermeldung erscheint
- [ ] Mehrfaches EnsureInitializedAsync() aufrufen → keine Exceptions

---

### ✅ Task 1.5: AlertsViewModel - Property Change Fire-and-Forget Fix
**Datei:** `src/DigitalSignage.Server/ViewModels/AlertsViewModel.cs`
**Priorität:** 🔴 CRITICAL
**Aufwand:** 1.5h
**Status:** ❌ TODO

**Änderungen:**
```csharp
private CancellationTokenSource? _filterChangeCts;

partial void OnSelectedFilterChanged(AlertFilterType value)
{
    // Cancel previous filter change
    _filterChangeCts?.Cancel();
    _filterChangeCts?.Dispose();
    _filterChangeCts = new CancellationTokenSource();

    var cts = _filterChangeCts;

    _ = Task.Run(async () =>
    {
        try
        {
            // Debounce: Wait 300ms to see if user changes filter again
            await Task.Delay(300, cts.Token);

            // Load alerts on UI thread
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await LoadAlertsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reload alerts after filter change");
                    await _dialogService.ShowErrorAsync(
                        $"Failed to reload alerts: {ex.Message}",
                        "Error");
                }
            });
        }
        catch (OperationCanceledException)
        {
            // User changed filter again - ignore
            _logger.LogDebug("Filter change cancelled (user changed filter again)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in filter change handler");
        }
    });
}

partial void OnFilterTextChanged(string value)
{
    // Same pattern as OnSelectedFilterChanged
    _filterChangeCts?.Cancel();
    _filterChangeCts?.Dispose();
    _filterChangeCts = new CancellationTokenSource();

    var cts = _filterChangeCts;

    _ = Task.Run(async () =>
    {
        try
        {
            await Task.Delay(300, cts.Token);  // Debounce for typing

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await LoadAlertsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reload alerts after filter text change");
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Filter text change cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in filter text change handler");
        }
    });
}

// Dispose pattern update
protected virtual void Dispose(bool disposing)
{
    if (_disposed) return;

    if (disposing)
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;

        // NEW: Cancel and dispose filter change token
        _filterChangeCts?.Cancel();
        _filterChangeCts?.Dispose();
        _filterChangeCts = null;

        _logger.LogInformation("AlertsViewModel disposed");
    }

    _disposed = true;
}
```

**Testplan:**
- [ ] Filter schnell ändern → nur letzte Änderung wird geladen
- [ ] Text schnell tippen → Debouncing funktioniert
- [ ] DB-Fehler → Fehlermeldung wird angezeigt

---

### ✅ Task 1.6: WebSocketCommunicationService - Background Task Tracking
**Datei:** `src/DigitalSignage.Server/Services/WebSocketCommunicationService.cs`
**Priorität:** 🔴 CRITICAL
**Aufwand:** 3h
**Status:** ❌ TODO

**Änderungen:**
```csharp
public class WebSocketCommunicationService : ICommunicationService, IDisposable
{
    private Task? _acceptClientsTask;
    private readonly ConcurrentBag<Task> _clientHandlerTasks = new();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // ... existing initialization code ...

        _httpListener.Start();

        // ✅ Track background task
        _acceptClientsTask = AcceptClientsAsync(_cancellationTokenSource.Token);

        // Monitor task for failures
        _ = MonitorBackgroundTaskAsync(_acceptClientsTask, "AcceptClients");

        _logger.LogInformation("WebSocket server started successfully");
    }

    private async Task MonitorBackgroundTaskAsync(Task task, string taskName)
    {
        try
        {
            await task;
            _logger.LogInformation("Background task {TaskName} completed normally", taskName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background task {TaskName} was cancelled", taskName);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Background task {TaskName} failed unexpectedly", taskName);
            // In production: Consider implementing restart logic or alerting
        }
    }

    private async Task HandleClientAsync(
        string clientId,
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        // Wrap in try-finally to remove from tracking
        try
        {
            // ... existing code ...
        }
        finally
        {
            // Cleanup is already in existing finally block
            _logger.LogDebug("Client handler task for {ClientId} completed", clientId);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping WebSocket communication service...");

        // 1. Signal cancellation
        _cancellationTokenSource?.Cancel();

        // 2. Stop accepting new connections
        _httpListener?.Stop();

        // 3. Wait for AcceptClientsAsync to complete (with timeout)
        if (_acceptClientsTask != null)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                await _acceptClientsTask.WaitAsync(linkedCts.Token);
                _logger.LogInformation("AcceptClientsAsync stopped gracefully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AcceptClientsAsync did not stop within timeout");
            }
        }

        // 4. Close all client connections gracefully
        var closeTasks = _clients.Values.Select(async client =>
        {
            try
            {
                if (client.State == WebSocketState.Open)
                {
                    await client.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server shutting down",
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing client WebSocket");
            }
        });

        await Task.WhenAll(closeTasks);
        _logger.LogInformation("All client connections closed");

        // 5. Clear clients
        _clients.Clear();

        // 6. Dispose resources
        _httpListener?.Close();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _logger.LogInformation("WebSocket communication service stopped successfully");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpListener?.Close();
            ((IDisposable?)_httpListener)?.Dispose();
            _cancellationTokenSource?.Dispose();
            _logger.LogInformation("WebSocketCommunicationService disposed");
        }
    }
}
```

**Testplan:**
- [ ] Server starten → AcceptClientsTask läuft
- [ ] Server stoppen → Task wird beendet
- [ ] Server crash simulieren → Task Monitoring loggt Fehler
- [ ] Multiple Clients verbinden → Handler Tasks werden getrackt

---

### ✅ Task 1.7: MessageHandlerService - Task Tracking
**Datei:** `src/DigitalSignage.Server/Services/MessageHandlerService.cs`
**Priorität:** 🔴 CRITICAL
**Aufwand:** 2.5h
**Status:** ❌ TODO

**Änderungen:**
```csharp
public class MessageHandlerService : BackgroundService
{
    private readonly ConcurrentBag<Task> _activeMessageTasks = new();
    private long _messageCounter = 0;

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        var messageId = Interlocked.Increment(ref _messageCounter);

        var task = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Processing message #{MessageId} from {ClientId}", messageId, e.ClientId);
                await HandleMessageAsync(e.ClientId, e.Message);
                _logger.LogDebug("Completed message #{MessageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message #{MessageId} from client {ClientId}",
                    messageId, e.ClientId);
            }
        });

        _activeMessageTasks.Add(task);

        // Cleanup completed tasks periodically
        if (_messageCounter % 100 == 0)
        {
            CleanupCompletedTasks();
        }
    }

    private void CleanupCompletedTasks()
    {
        var tasks = _activeMessageTasks.ToArray();
        var completedCount = 0;

        foreach (var task in tasks)
        {
            if (task.IsCompleted)
            {
                _activeMessageTasks.TryTake(out _);
                completedCount++;
            }
        }

        if (completedCount > 0)
        {
            _logger.LogDebug("Cleaned up {Count} completed message handler tasks", completedCount);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Message Handler Service stopping...");

        // Unsubscribe from events (no new messages)
        _communicationService.MessageReceived -= OnMessageReceived;
        _communicationService.ClientDisconnected -= OnClientDisconnected;

        // Wait for active message handlers to complete
        var activeTasks = _activeMessageTasks.ToArray();
        if (activeTasks.Length > 0)
        {
            _logger.LogInformation(
                "Waiting for {Count} active message handler tasks to complete...",
                activeTasks.Length);

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                await Task.WhenAll(activeTasks).WaitAsync(linkedCts.Token);
                _logger.LogInformation("All message handler tasks completed gracefully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Some message handler tasks did not complete within timeout. " +
                    "Completed: {Completed}/{Total}",
                    activeTasks.Count(t => t.IsCompleted),
                    activeTasks.Length);
            }
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Message Handler Service stopped");
    }

    // Better async void for event handler
    private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation(
                    "Client {ClientId} disconnected from WebSocket - marking as offline",
                    e.ClientId);

                await _clientService.UpdateClientStatusAsync(
                    e.ClientId,
                    ClientStatus.Offline);

                _logger.LogInformation("Client {ClientId} status updated to Offline", e.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling disconnect for client {ClientId}", e.ClientId);
            }
        });
    }
}
```

**Testplan:**
- [ ] Message Handler Service starten
- [ ] 100 Messages senden → Cleanup wird ausgeführt
- [ ] Service stoppen mit aktiven Tasks → Graceful shutdown
- [ ] Service stoppen mit Timeout → Warning wird geloggt

---

### ✅ Task 1.8: ClientService - Initialization Tracking
**Datei:** `src/DigitalSignage.Server/Services/ClientService.cs`
**Priorität:** 🔴 CRITICAL
**Aufwand:** 3h
**Status:** ❌ TODO

**Änderungen:**
```csharp
public class ClientService : IClientService, IDisposable
{
    private Task? _initializationTask;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _isInitialized = false;
    private bool _disposed = false;

    public ClientService(...)
    {
        // ... existing assignments ...

        // Start initialization but track it
        _initializationTask = InitializeClientsWithRetryAsync();
    }

    private async Task InitializeClientsWithRetryAsync()
    {
        var maxRetries = 10;
        var delayMs = 500;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await InitializeClientsAsync();
                _logger.LogInformation("Clients initialized successfully on attempt {Attempt}", attempt);
                return; // Success
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        "Failed to load clients from database (attempt {Attempt}/{MaxRetries}): {Message}. " +
                        "Retrying in {DelayMs}ms...",
                        attempt, maxRetries, ex.Message, delayMs);

                    await Task.Delay(delayMs);
                    delayMs = Math.Min(delayMs * 2, 5000);
                }
                else
                {
                    _logger.LogError(ex,
                        "Failed to load clients from database after {MaxRetries} attempts. " +
                        "Service will continue without pre-loaded clients.",
                        maxRetries);

                    // Mark as initialized even if it failed
                    // (prevents methods from hanging forever)
                    _isInitialized = true;
                }
            }
        }
    }

    /// <summary>
    /// Ensure initialization is complete before proceeding
    /// </summary>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ClientService));

        if (_isInitialized)
            return;

        if (_initializationTask != null)
        {
            try
            {
                await _initializationTask.WaitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initialization task failed");
                // Continue - service is still usable
            }
        }
    }

    /// <summary>
    /// Check if initialization is complete (non-blocking)
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Get initialization status and any errors
    /// </summary>
    public (bool IsComplete, bool HasErrors, Exception? Error) GetInitializationStatus()
    {
        if (_initializationTask == null)
            return (false, false, null);

        var isComplete = _initializationTask.IsCompleted;
        var hasErrors = _initializationTask.IsFaulted;
        var error = _initializationTask.Exception?.GetBaseException();

        return (isComplete, hasErrors, error);
    }

    // Update all public methods to ensure initialization
    public async Task<List<RaspberryPiClient>> GetAllClientsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            // ... existing implementation ...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all clients");
            return _clients.Values.ToList(); // Return cached data as fallback
        }
    }

    // Same pattern for other public methods...

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _initSemaphore?.Dispose();
            _logger.LogInformation("ClientService disposed");
        }

        _disposed = true;
    }
}
```

**IClientService Interface Update:**
```csharp
public interface IClientService
{
    // Existing methods...

    // NEW: Initialization methods
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    bool IsInitialized { get; }
    (bool IsComplete, bool HasErrors, Exception? Error) GetInitializationStatus();
}
```

**Testplan:**
- [ ] Service erstellen → Initialization startet
- [ ] GetAllClientsAsync() vor Init complete → wartet auf Init
- [ ] DB nicht verfügbar → Retry funktioniert
- [ ] Retry fehlschlägt → Service ist trotzdem verwendbar
- [ ] GetInitializationStatus() → korrekte Werte

---

## 📌 PHASE 2: ROBUSTHEIT (PRIORITÄT 2) - 20h

### ✅ Task 2.1: Error Handling Standardization
**Priorität:** 🟡 HIGH
**Aufwand:** 8h
**Status:** ❌ TODO

**Aufgabe:**
Standardisiertes Error Handling für alle Services implementieren

**Änderungen:**
1. Base Exception Types erstellen:
```csharp
// src/DigitalSignage.Core/Exceptions/DigitalSignageException.cs
public abstract class DigitalSignageException : Exception
{
    protected DigitalSignageException(string message) : base(message) { }
    protected DigitalSignageException(string message, Exception innerException)
        : base(message, innerException) { }
}

public class ServiceInitializationException : DigitalSignageException
{
    public ServiceInitializationException(string serviceName, Exception innerException)
        : base($"Failed to initialize {serviceName}", innerException) { }
}

public class ClientNotFoundException : DigitalSignageException
{
    public string ClientId { get; }

    public ClientNotFoundException(string clientId)
        : base($"Client '{clientId}' not found")
    {
        ClientId = clientId;
    }
}

public class LayoutNotFoundException : DigitalSignageException
{
    public string LayoutId { get; }

    public LayoutNotFoundException(string layoutId)
        : base($"Layout '{layoutId}' not found")
    {
        LayoutId = layoutId;
    }
}
```

2. Service Error Handling Pattern:
```csharp
public async Task<Result<DisplayLayout>> CreateLayoutAsync(
    DisplayLayout layout,
    CancellationToken cancellationToken = default)
{
    try
    {
        ThrowIfDisposed();

        if (layout == null)
            return Result<DisplayLayout>.Failure("Layout cannot be null");

        if (string.IsNullOrWhiteSpace(layout.Name))
            return Result<DisplayLayout>.Failure("Layout name is required");

        // ... implementation ...

        return Result<DisplayLayout>.Success(layout);
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Layout creation cancelled");
        return Result<DisplayLayout>.Failure("Operation was cancelled");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create layout {LayoutName}", layout?.Name);
        return Result<DisplayLayout>.Failure($"Failed to create layout: {ex.Message}");
    }
}
```

3. Result<T> Type erstellen:
```csharp
// src/DigitalSignage.Core/Models/Result.cs
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private Result(bool isSuccess, T? value, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);

    public static Result<T> Failure(string errorMessage, Exception? exception = null)
        => new(false, default, errorMessage, exception);
}
```

**Betroffene Dateien:**
- Alle Services in `src/DigitalSignage.Server/Services/`
- Alle ViewModels in `src/DigitalSignage.Server/ViewModels/`

**Testplan:**
- [ ] Exception Types erstellen und testen
- [ ] Result<T> Type implementieren
- [ ] 3 Services als Beispiel konvertieren
- [ ] Unit Tests für Error Handling

---

### ✅ Task 2.2: Input Validation Framework
**Priorität:** 🟡 HIGH
**Aufwand:** 6h
**Status:** ❌ TODO

**Aufgabe:**
Systematische Input Validation für alle öffentlichen APIs

**Implementierung:**
```csharp
// src/DigitalSignage.Core/Validation/Validator.cs
public static class Validator
{
    public static ValidationResult ValidateClientId(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return ValidationResult.Failure("Client ID is required");

        if (clientId.Length > 255)
            return ValidationResult.Failure("Client ID too long (max 255 characters)");

        if (!Guid.TryParse(clientId, out _))
            return ValidationResult.Failure("Client ID must be a valid GUID");

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateMacAddress(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return ValidationResult.Failure("MAC address is required");

        var regex = new Regex(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$");
        if (!regex.IsMatch(macAddress))
            return ValidationResult.Failure("Invalid MAC address format (expected: XX:XX:XX:XX:XX:XX)");

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateLayoutName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ValidationResult.Failure("Layout name is required");

        if (name.Length > 200)
            return ValidationResult.Failure("Layout name too long (max 200 characters)");

        if (name.Any(c => Path.GetInvalidFileNameChars().Contains(c)))
            return ValidationResult.Failure("Layout name contains invalid characters");

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return ValidationResult.Failure("IP address is required");

        if (!IPAddress.TryParse(ipAddress, out _))
            return ValidationResult.Failure("Invalid IP address format");

        return ValidationResult.Success();
    }
}

public class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Failure(string message) => new(false, message);
}
```

**Usage in Services:**
```csharp
public async Task<Result<RaspberryPiClient>> RegisterClientAsync(
    RegisterMessage registerMessage,
    CancellationToken cancellationToken = default)
{
    // Validate input
    var macValidation = Validator.ValidateMacAddress(registerMessage.MacAddress);
    if (!macValidation.IsValid)
        return Result<RaspberryPiClient>.Failure(macValidation.ErrorMessage!);

    if (!string.IsNullOrWhiteSpace(registerMessage.ClientId))
    {
        var clientIdValidation = Validator.ValidateClientId(registerMessage.ClientId);
        if (!clientIdValidation.IsValid)
            return Result<RaspberryPiClient>.Failure(clientIdValidation.ErrorMessage!);
    }

    // ... rest of implementation ...
}
```

**Testplan:**
- [ ] Validator class mit Unit Tests
- [ ] Integration in ClientService
- [ ] Integration in LayoutService
- [ ] Invalid input tests

---

### ✅ Task 2.3: Async Void Robustness
**Priorität:** 🟡 HIGH
**Aufwand:** 4h
**Status:** ❌ TODO

**Aufgabe:**
Alle async void Event Handler robuster machen

**Pattern:**
```csharp
// XAML Event Handler Pattern
private async void OnButtonClick(object sender, RoutedEventArgs e)
{
    // Disable button to prevent double-click
    var button = sender as Button;
    if (button != null)
        button.IsEnabled = false;

    try
    {
        await PerformActionAsync();
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Operation cancelled by user");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in button click handler");

        await ShowErrorMessageAsync(
            "Operation Failed",
            $"An error occurred: {ex.Message}");
    }
    finally
    {
        // Re-enable button
        if (button != null)
            button.IsEnabled = true;
    }
}
```

**Betroffene Dateien:**
- `src/DigitalSignage.Server/Views/Dialogs/SettingsDialog.xaml.cs`
- `src/DigitalSignage.Server/Controls/TablePropertiesControl.xaml.cs`
- `src/DigitalSignage.Server/Views/DatabaseConnectionDialog.xaml.cs`
- `src/DigitalSignage.Server/Views/Dialogs/MediaBrowserDialog.xaml.cs`

**Testplan:**
- [ ] Alle async void Handler haben try-catch
- [ ] Exception Handling testet
- [ ] UI bleibt responsive bei Fehler

---

### ✅ Task 2.4: Logging Enhancement
**Priorität:** 🟡 HIGH
**Aufwand:** 2h
**Status:** ❌ TODO

**Aufgabe:**
Logging-Qualität verbessern mit strukturierten Logs

**Änderungen:**
```csharp
// ❌ BAD
_logger.LogError("Failed to load client " + clientId);

// ✅ GOOD
_logger.LogError("Failed to load client {ClientId}", clientId);

// ❌ BAD
_logger.LogInformation($"Processing {count} items");

// ✅ GOOD
_logger.LogInformation("Processing {ItemCount} items", count);

// ❌ BAD - Exception wird nicht geloggt
catch (Exception ex)
{
    _logger.LogError("Failed to save layout");
}

// ✅ GOOD - Exception wird mitgeloggt
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to save layout {LayoutId}", layoutId);
}
```

**Log Scopes verwenden:**
```csharp
public async Task<bool> AssignLayoutAsync(string clientId, string layoutId, ...)
{
    using var scope = _logger.BeginScope(
        "AssignLayout: ClientId={ClientId}, LayoutId={LayoutId}",
        clientId, layoutId);

    _logger.LogInformation("Starting layout assignment");

    try
    {
        // ... implementation ...
        _logger.LogInformation("Layout assignment successful");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Layout assignment failed");
        return false;
    }
}
```

**Testplan:**
- [ ] Alle string concatenation durch structured logging ersetzen
- [ ] Log scopes für komplexe Operationen
- [ ] Serilog enrichers testen

---

## 📌 PHASE 3: PERFORMANCE (PRIORITÄT 3) - 20h

### ✅ Task 3.1: LINQ Optimization - Multiple Iterations
**Priorität:** 🟢 MEDIUM
**Aufwand:** 8h
**Status:** ❌ TODO

**Aufgabe:**
Alle mehrfachen LINQ-Iterationen durch Single-Pass-Operationen ersetzen

**Pattern-Search:**
Suche in allen .cs Dateien nach:
- Multiple `.Count(predicate)` calls auf gleicher Collection
- Multiple `.Where()` calls auf gleicher Collection
- `.ToList().Count` statt `.Count()`

**Beispiel-Fix:**
```csharp
// ❌ BAD - 5 iterations
public LogStatistics GetStatistics()
{
    var allLogs = _allLogs.ToArray();
    return new LogStatistics
    {
        DebugCount = allLogs.Count(l => l.Level == LogLevel.Debug),
        InfoCount = allLogs.Count(l => l.Level == LogLevel.Info),
        WarningCount = allLogs.Count(l => l.Level == LogLevel.Warning),
        ErrorCount = allLogs.Count(l => l.Level == LogLevel.Error),
        CriticalCount = allLogs.Count(l => l.Level == LogLevel.Critical)
    };
}

// ✅ GOOD - 1 iteration with GroupBy
public LogStatistics GetStatistics()
{
    var allLogs = _allLogs.ToArray();
    var levelCounts = allLogs
        .GroupBy(l => l.Level)
        .ToDictionary(g => g.Key, g => g.Count());

    return new LogStatistics
    {
        DebugCount = levelCounts.GetValueOrDefault(LogLevel.Debug, 0),
        InfoCount = levelCounts.GetValueOrDefault(LogLevel.Info, 0),
        WarningCount = levelCounts.GetValueOrDefault(LogLevel.Warning, 0),
        ErrorCount = levelCounts.GetValueOrDefault(LogLevel.Error, 0),
        CriticalCount = levelCounts.GetValueOrDefault(LogLevel.Critical, 0),
        TotalCount = allLogs.Length
    };
}
```

**Betroffene Bereiche:**
- Statistics-Berechnungen
- Filter-Operationen in ViewModels
- Data Aggregation in Services

**Testplan:**
- [ ] Benchmark erstellen (BenchmarkDotNet)
- [ ] Vorher/Nachher Performance messen
- [ ] Korrektheit der Ergebnisse prüfen

---

### ✅ Task 3.2: String Allocation Optimization
**Priorität:** 🟢 MEDIUM
**Aufwand:** 4h
**Status:** ❌ TODO

**Aufgabe:**
Unnötige String Allocations reduzieren

**Patterns zu fixen:**
```csharp
// ❌ BAD
var message = "Prefix: " + value + " Suffix";  // 3 allocations

// ✅ GOOD
var message = $"Prefix: {value} Suffix";  // 1 allocation

// ✅ BETTER for logging
_logger.LogInformation("Prefix: {Value} Suffix", value);  // Only if logged!


// ❌ BAD - in loop
for (int i = 0; i < 1000; i++)
{
    var id = "item-" + i.ToString();  // 2000 allocations!
}

// ✅ GOOD - StringBuilder in loop
var sb = new StringBuilder();
for (int i = 0; i < 1000; i++)
{
    sb.Clear();
    sb.Append("item-");
    sb.Append(i);
    var id = sb.ToString();  // 1000 allocations
}

// ✅ BEST - string interpolation
for (int i = 0; i < 1000; i++)
{
    var id = $"item-{i}";  // 1000 allocations (optimized by compiler)
}
```

**Testplan:**
- [ ] Memory Profiler vor/nach
- [ ] Allocation Benchmarks

---

### ✅ Task 3.3: Collection Initialization Optimization
**Priorität:** 🟢 MEDIUM
**Aufwand:** 3h
**Status:** ❌ TODO

**Aufgabe:**
Collection-Initialisierungen mit bekannter Kapazität optimieren

**Pattern:**
```csharp
// ❌ BAD - grows dynamically
var list = new List<DisplayElement>();
foreach (var item in largeCollection)
{
    list.Add(ProcessElement(item));  // Multiple array resizes!
}

// ✅ GOOD - pre-sized
var list = new List<DisplayElement>(largeCollection.Count);
foreach (var item in largeCollection)
{
    list.Add(ProcessElement(item));  // No resizing needed
}

// ✅ BETTER - LINQ
var list = largeCollection.Select(ProcessElement).ToList();
```

**Testplan:**
- [ ] Identify collections with known sizes
- [ ] Add capacity parameter
- [ ] Measure memory savings

---

### ✅ Task 3.4: Async Stream für große Datenmengen
**Priorität:** 🟢 MEDIUM
**Aufwand:** 5h
**Status:** ❌ TODO

**Aufgabe:**
IAsyncEnumerable für große Result Sets

**Implementation:**
```csharp
// ❌ BAD - loads all in memory
public async Task<List<Alert>> GetAllAlertsAsync()
{
    await using var context = await _contextFactory.CreateDbContextAsync();
    return await context.Alerts.ToListAsync();  // Kann Millionen sein!
}

// ✅ GOOD - streaming
public async IAsyncEnumerable<Alert> GetAllAlertsStreamAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await using var context = await _contextFactory.CreateDbContextAsync();

    await foreach (var alert in context.Alerts.AsAsyncEnumerable().WithCancellation(cancellationToken))
    {
        yield return alert;
    }
}

// Usage
await foreach (var alert in alertService.GetAllAlertsStreamAsync())
{
    ProcessAlert(alert);  // Memory-efficient
}
```

**Testplan:**
- [ ] Large dataset test (10k+ records)
- [ ] Memory usage comparison
- [ ] Performance benchmarks

---

## 📌 PHASE 4: CODE QUALITY (PRIORITÄT 4) - 16h

### ✅ Task 4.1: Magic Numbers Elimination
**Priorität:** 🟢 MEDIUM
**Aufwand:** 4h
**Status:** ❌ TODO

**Aufgabe:**
Alle Magic Numbers durch named constants ersetzen

**Configuration Class erstellen:**
```csharp
// src/DigitalSignage.Server/Configuration/ServiceConstants.cs
public static class WebSocketConstants
{
    /// <summary>
    /// WebSocket receive buffer size (8KB - standard frame size)
    /// </summary>
    public const int BufferSize = 8192;

    /// <summary>
    /// Maximum message size before compression (1MB)
    /// </summary>
    public const int CompressionThreshold = 1_048_576;

    /// <summary>
    /// Graceful shutdown timeout for background tasks
    /// </summary>
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);
}

public static class NetworkConstants
{
    /// <summary>
    /// Ping timeout for network discovery (500ms)
    /// </summary>
    public const int PingTimeoutMs = 500;

    /// <summary>
    /// Concurrent ping batch size to avoid network flooding
    /// </summary>
    public const int PingBatchSize = 50;

    /// <summary>
    /// UDP discovery port for client broadcast
    /// </summary>
    public const int DiscoveryPort = 5556;
}

public static class ServiceRetryConstants
{
    /// <summary>
    /// Initial retry delay for service initialization
    /// </summary>
    public const int InitialRetryDelayMs = 500;

    /// <summary>
    /// Maximum retry delay (exponential backoff cap)
    /// </summary>
    public const int MaxRetryDelayMs = 5000;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public const int MaxRetryAttempts = 10;
}
```

**Usage:**
```csharp
// ❌ BEFORE
var buffer = new byte[8192];
var reply = await ping.SendPingAsync(ipAddress, 500);

// ✅ AFTER
var buffer = new byte[WebSocketConstants.BufferSize];
var reply = await ping.SendPingAsync(ipAddress, NetworkConstants.PingTimeoutMs);
```

**Testplan:**
- [ ] Search für hardcoded numbers
- [ ] Constants class erstellen
- [ ] Alle Vorkommen ersetzen

---

### ✅ Task 4.2: Base Class für Disposable Services
**Priorität:** 🟢 MEDIUM
**Aufwand:** 5h
**Status:** ❌ TODO

**Implementation:**
```csharp
// src/DigitalSignage.Server/Services/DisposableService.cs
public abstract class DisposableService : IDisposable
{
    private bool _disposed = false;
    protected readonly ILogger Logger;

    protected DisposableService(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                DisposeManagedResources();
                Logger.LogInformation("{ServiceName} disposed", GetType().Name);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error disposing {ServiceName}", GetType().Name);
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Override this method to dispose managed resources
    /// </summary>
    protected abstract void DisposeManagedResources();

    /// <summary>
    /// Throws if service has been disposed
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    /// <summary>
    /// Check if service is disposed (for internal use)
    /// </summary>
    protected bool IsDisposed => _disposed;
}
```

**Usage:**
```csharp
public class LayoutService : DisposableService, ILayoutService
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public LayoutService(ILogger<LayoutService> logger) : base(logger)
    {
    }

    protected override void DisposeManagedResources()
    {
        _fileLock?.Dispose();
    }

    public async Task<DisplayLayout> CreateLayoutAsync(...)
    {
        ThrowIfDisposed();
        // ... implementation ...
    }
}
```

**Testplan:**
- [ ] Base class erstellen
- [ ] 3 Services konvertieren als Beispiel
- [ ] Dispose-Tests

---

### ✅ Task 4.3: XML Documentation Vervollständigung
**Priorität:** 🟢 MEDIUM
**Aufwand:** 5h
**Status:** ❌ TODO

**Aufgabe:**
Alle public APIs mit XML-Dokumentation versehen

**Template:**
```csharp
/// <summary>
/// Creates a new layout with the specified configuration
/// </summary>
/// <param name="layout">The layout to create</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The created layout with generated ID and timestamps</returns>
/// <exception cref="ArgumentNullException">Thrown when layout is null</exception>
/// <exception cref="ArgumentException">Thrown when layout name is empty</exception>
/// <exception cref="ObjectDisposedException">Thrown when service is disposed</exception>
/// <exception cref="InvalidOperationException">Thrown when layout creation fails</exception>
/// <example>
/// <code>
/// var layout = new DisplayLayout { Name = "Welcome Screen", Width = 1920, Height = 1080 };
/// var created = await layoutService.CreateLayoutAsync(layout);
/// </code>
/// </example>
public async Task<DisplayLayout> CreateLayoutAsync(
    DisplayLayout layout,
    CancellationToken cancellationToken = default)
{
    // ...
}
```

**Coverage Ziel:**
- [ ] 100% public interfaces
- [ ] 100% public classes
- [ ] 100% public methods
- [ ] 80% public properties

---

### ✅ Task 4.4: Code Duplication Elimination
**Priorität:** 🟢 MEDIUM
**Aufwand:** 2h
**Status:** ❌ TODO

**Aufgabe:**
Duplicate Code identifizieren und refactoren

**Tool:** Use Resharper Code Inspection oder `dotnet-dupfinder`

**Common Patterns:**
- DbContext Usage Pattern
- Error Handling Pattern
- Validation Pattern
- Logging Pattern

**Testplan:**
- [ ] Run duplication detection
- [ ] Extract common methods
- [ ] Verify refactoring didn't break tests

---

## 📊 PROGRESS TRACKING

### Completion Status

| Phase | Tasks | Completed | Progress |
|-------|-------|-----------|----------|
| Phase 1 (Critical) | 8 | 0 | 0% |
| Phase 2 (High) | 4 | 0 | 0% |
| Phase 3 (Medium) | 4 | 0 | 0% |
| Phase 4 (Low) | 4 | 0 | 0% |
| **TOTAL** | **20** | **0** | **0%** |

### Time Tracking

| Phase | Estimated | Actual | Remaining |
|-------|-----------|--------|-----------|
| Phase 1 | 16h | 0h | 16h |
| Phase 2 | 20h | 0h | 20h |
| Phase 3 | 20h | 0h | 20h |
| Phase 4 | 16h | 0h | 16h |
| **TOTAL** | **72h** | **0h** | **72h** |

---

## 🔄 CONTINUOUS INTEGRATION

### Nach jedem Task:

1. ✅ **Build erfolgreich:** `dotnet build`
2. ✅ **Tests grün:** `dotnet test`
3. ✅ **Code Quality:** Run static analysis
4. ✅ **Commit:** Mit aussagekräftiger Message
5. ✅ **Push:** Zu GitHub

### Nach jeder Phase:

1. ✅ **Integration Tests:** Alle Services zusammen testen
2. ✅ **Performance Tests:** Benchmarks ausführen
3. ✅ **Code Review:** Peer review durchführen
4. ✅ **Documentation Update:** CHANGELOG.md aktualisieren

---

## 📝 NOTES

- **Branch Strategy:** Feature-Branch pro Phase (`feature/phase-1-critical-fixes`)
- **Testing:** Unit Tests für jede Änderung
- **Review:** Code Review nach jeder Phase
- **Deployment:** Erst nach Phase 1 + 2 auf Production

---

**Erstellt am:** 2025-11-15
**Nächstes Update:** Nach Abschluss Phase 1
