using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Utilities;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for actively scanning the local network for potential Digital Signage clients
/// </summary>
public enum NetworkScanMode
{
    Quick,
    Deep
}

/// <summary>
/// Service for actively scanning the local network for potential Digital Signage clients
/// </summary>
public class NetworkScannerService : IDisposable
{
    private readonly ILogger<NetworkScannerService> _logger;
    private readonly ConcurrentDictionary<string, DiscoveredDevice> _discoveredDevices = new();
    private bool _isScanning = false;
    private readonly SemaphoreSlim _scanningSemaphore = new(1, 1);
    private bool _disposed = false;

    /// <summary>
    /// Event raised when a new device is discovered
    /// </summary>
    public event EventHandler<DiscoveredDevice>? DeviceDiscovered;

    /// <summary>
    /// Event raised when scanning status changes
    /// </summary>
    public event EventHandler<bool>? ScanningStatusChanged;

    public NetworkScannerService(ILogger<NetworkScannerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets whether a scan is currently in progress
    /// </summary>
    public bool IsScanning => _isScanning;

    /// <summary>
    /// Gets all currently discovered devices
    /// </summary>
    public IEnumerable<DiscoveredDevice> GetDiscoveredDevices()
    {
        return _discoveredDevices.Values.OrderByDescending(d => d.DiscoveredAt);
    }

    /// <summary>
    /// Clear all discovered devices
    /// </summary>
    public void ClearDiscoveredDevices()
    {
        _discoveredDevices.Clear();
        _logger.LogInformation("Cleared all discovered devices");
    }

    /// <summary>
    /// Remove devices discovered before the specified time
    /// </summary>
    public void RemoveStaleDevices(TimeSpan maxAge)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        var staleDevices = _discoveredDevices.Where(kvp => kvp.Value.DiscoveredAt < cutoffTime).ToList();

        foreach (var stale in staleDevices)
        {
            _discoveredDevices.TryRemove(stale.Key, out _);
        }

        if (staleDevices.Any())
        {
            _logger.LogInformation("Removed {Count} stale discovered devices", staleDevices.Count);
        }
    }

    /// <summary>
    /// Remove a specific discovered device
    /// </summary>
    public bool RemoveDiscoveredDevice(string ipAddress)
    {
        if (_discoveredDevices.TryRemove(ipAddress, out _))
        {
            _logger.LogInformation("Removed discovered device: {IpAddress}", ipAddress);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Start scanning the local network for devices
    /// </summary>
    public async Task<int> ScanNetworkAsync(NetworkScanMode scanMode = NetworkScanMode.Quick, CancellationToken cancellationToken = default)
    {
        await _scanningSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_isScanning)
            {
                _logger.LogWarning("Network scan already in progress");
                return 0;
            }

            _isScanning = true;
            ScanningStatusChanged?.Invoke(this, true);
            _logger.LogInformation("Starting network scan with mode {Mode}...", scanMode);

            var discoveredCount = 0;

            // Get local network information
            var localIPs = GetLocalIPAddresses();
            _logger.LogInformation("Found {Count} local network interface(s)", localIPs.Length);

            foreach (var localIP in localIPs)
            {
                try
                {
                    var subnet = GetSubnet(localIP);
                    _logger.LogInformation("Scanning subnet: {Subnet}", subnet);

                    var tasks = new List<Task>();
                    for (int i = 1; i <= 254; i++)
                    {
                        var ip = $"{subnet}.{i}";

                        // Skip local IP
                        if (localIPs.Any(local => local.ToString() == ip))
                            continue;

                        tasks.Add(ScanHostAsync(ip, scanMode, cancellationToken));

                        // Batch scanning to avoid overwhelming the network
                        if (tasks.Count >= 50)
                        {
                            await Task.WhenAll(tasks);
                            tasks.Clear();
                        }
                    }

                    // Wait for remaining tasks
                    if (tasks.Any())
                    {
                        await Task.WhenAll(tasks);
                    }

                    discoveredCount = _discoveredDevices.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning subnet for local IP {LocalIP}", localIP);
                }
            }

            if (scanMode == NetworkScanMode.Deep)
            {
                var udpCount = await ScanUsingUdpDiscoveryAsync(timeoutSeconds: 3, cancellationToken);
                if (udpCount > 0)
                {
                    _logger.LogDebug("Deep scan UDP discovery added {Count} device(s)", udpCount);
                }
            }

            discoveredCount = _discoveredDevices.Count;
            _logger.LogInformation("Network scan completed. Discovered {Count} device(s)", discoveredCount);
            return discoveredCount;
        }
        finally
        {
            _isScanning = false;
            ScanningStatusChanged?.Invoke(this, false);
            _scanningSemaphore.Release();
        }
    }

    /// <summary>
    /// Scan a specific host
    /// </summary>
    private async Task ScanHostAsync(string ipAddress, NetworkScanMode scanMode, CancellationToken cancellationToken)
    {
        try
        {
            // First attempt: ICMP ping
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 500); // 500ms timeout

            var reachable = reply.Status == IPStatus.Success;
            string discoveryMethod = "Ping Scan";
            int? openPort = null;

            // Optional follow-up in Deep mode: probe TCP ports to improve detection (even if ping responded)
            if (scanMode == NetworkScanMode.Deep)
            {
                var probed = await ProbeTcpPortsAsync(ipAddress, cancellationToken);
                reachable = reachable || probed.reachable;
                openPort = probed.port;
                if (probed.reachable)
                {
                    discoveryMethod = openPort.HasValue ? $"Deep TCP {openPort}" : "Deep TCP probe";
                }
            }

            if (!reachable)
            {
                return;
            }

            var device = new DiscoveredDevice
            {
                IpAddress = ipAddress,
                DiscoveredAt = DateTime.UtcNow,
                DiscoveryMethod = discoveryMethod,
                IsReachable = true
            };

            // Try to resolve hostname
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                device.Hostname = hostEntry.HostName;

                // Check if it's likely a Raspberry Pi based on hostname
                device.IsLikelyRaspberryPi = IsLikelyRaspberryPi(hostEntry.HostName);
            }
            catch
            {
                device.Hostname = ipAddress; // Fallback to IP
            }

            // Try to get MAC address (requires admin privileges on Windows)
            try
            {
                device.MacAddress = await GetMacAddressAsync(ipAddress);
            }
            catch
            {
                // MAC address lookup failed - not critical
            }

            // Add or update device
            if (_discoveredDevices.TryAdd(ipAddress, device))
            {
                _logger.LogDebug("Discovered device: {Hostname} ({IpAddress})", device.Hostname, device.IpAddress);
                DeviceDiscovered?.Invoke(this, device);
            }
            else
            {
                // Update existing entry
                _discoveredDevices[ipAddress] = device;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error scanning host {IpAddress}", ipAddress);
        }
    }

    /// <summary>
    /// Get all local IPv4 addresses
    /// </summary>
    private IPAddress[] GetLocalIPAddresses()
    {
        return NetworkUtilities.GetLocalIPv4Addresses();
    }

    /// <summary>
    /// Get subnet prefix from IP address (e.g., "192.168.1" from "192.168.1.100")
    /// </summary>
    private string GetSubnet(IPAddress ipAddress)
    {
        var parts = ipAddress.ToString().Split('.');
        return $"{parts[0]}.{parts[1]}.{parts[2]}";
    }

    /// <summary>
    /// Get MAC address for an IP address using ARP table
    /// </summary>
    private async Task<string?> GetMacAddressAsync(string ipAddress)
    {
        try
        {
            // This is platform-specific and may require admin privileges
            // For now, return null as it's not critical
            await Task.CompletedTask;
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if hostname indicates a Raspberry Pi
    /// </summary>
    private bool IsLikelyRaspberryPi(string hostname)
    {
        var indicators = new[] { "raspberry", "raspberrypi", "pi-", "rpi", "digitalsignage" };
        var lower = hostname.ToLowerInvariant();
        return indicators.Any(indicator => lower.Contains(indicator));
    }

    /// <summary>
    /// Scan for devices using UDP broadcast discovery (Digital Signage specific)
    /// </summary>
    public async Task<int> ScanUsingUdpDiscoveryAsync(int timeoutSeconds = 5, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting UDP broadcast discovery scan (timeout: {Timeout}s)", timeoutSeconds);

        try
        {
            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

            // Send broadcast discovery request
            var discoveryRequest = System.Text.Encoding.UTF8.GetBytes("DIGITALSIGNAGE_DISCOVER_CLIENT");
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, 5556); // Client discovery port

            await udpClient.SendAsync(discoveryRequest, discoveryRequest.Length, broadcastEndpoint);
            _logger.LogDebug("Sent UDP broadcast discovery request");

            // Listen for responses with timeout
            udpClient.Client.ReceiveTimeout = timeoutSeconds * 1000;

            var startTime = DateTime.UtcNow;
            var discoveredCount = 0;

            while ((DateTime.UtcNow - startTime).TotalSeconds < timeoutSeconds && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Create a task that will timeout after 1 second
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    var result = await udpClient.ReceiveAsync(linkedCts.Token);
                    var message = System.Text.Encoding.UTF8.GetString(result.Buffer);

                    _logger.LogDebug("Received UDP response from {RemoteEndPoint}: {Message}", result.RemoteEndPoint, message);

                    // Parse response and add to discovered devices
                    var device = new DiscoveredDevice
                    {
                        IpAddress = result.RemoteEndPoint.Address.ToString(),
                        Hostname = result.RemoteEndPoint.Address.ToString(),
                        DiscoveredAt = DateTime.UtcNow,
                        DiscoveryMethod = "UDP Broadcast",
                        IsReachable = true
                    };

                    if (_discoveredDevices.TryAdd(device.IpAddress, device))
                    {
                        discoveredCount++;
                        DeviceDiscovered?.Invoke(this, device);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timeout or cancellation - continue or break
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    // Otherwise it was just a timeout, continue listening
                }
                catch (SocketException)
                {
                    // No more data available
                    break;
                }
            }

            _logger.LogInformation("UDP discovery scan completed. Discovered {Count} device(s)", discoveredCount);
            return discoveredCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during UDP discovery scan");
            return 0;
        }
    }

    private async Task<(bool reachable, int? port)> ProbeTcpPortsAsync(string ipAddress, CancellationToken cancellationToken)
    {
        var portsToProbe = new[] { 22, 80, 443, 8080 };

        foreach (var port in portsToProbe)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(ipAddress, port);
                var delayTask = Task.Delay(500, cancellationToken);

                var completed = await Task.WhenAny(connectTask, delayTask);

                if (completed == connectTask && tcpClient.Connected)
                {
                    return (true, port);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "TCP probe failed for {IpAddress}:{Port}", ipAddress, port);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _scanningSemaphore?.Dispose();
        _disposed = true;
    }
}
