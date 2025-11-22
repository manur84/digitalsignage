using System.Collections.Concurrent;
using DigitalSignage.App.Mobile.Models;
using Zeroconf;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Implementation of server discovery service using mDNS (Zeroconf/Bonjour).
/// </summary>
public class ServerDiscoveryService : IServerDiscoveryService
{
	private const string ServiceType = "_digitalsignage._tcp.local.";
	private const int ScanTimeoutSeconds = 10;

	private readonly ConcurrentDictionary<string, DiscoveredServer> _discoveredServers = new();
	private CancellationTokenSource? _scanCancellationTokenSource;

	/// <inheritdoc/>
	public event EventHandler<DiscoveredServer>? ServerDiscovered;

	/// <inheritdoc/>
	public event EventHandler<DiscoveredServer>? ServerLost;

	/// <inheritdoc/>
	public bool IsScanning => _scanCancellationTokenSource != null && !_scanCancellationTokenSource.IsCancellationRequested;

	/// <inheritdoc/>
	public async Task StartScanningAsync(CancellationToken cancellationToken = default)
	{
		if (IsScanning)
		{
			Console.WriteLine("Server discovery already running");
			return;
		}

		try
		{
			_scanCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			Console.WriteLine($"Starting mDNS scan for service type: {ServiceType}");

			// Clear old servers
			_discoveredServers.Clear();

			// Start scanning
			var responses = await ZeroconfResolver.ResolveAsync(
				ServiceType,
				scanTime: TimeSpan.FromSeconds(ScanTimeoutSeconds),
				cancellationToken: _scanCancellationTokenSource.Token);

			foreach (var response in responses)
			{
				if (_scanCancellationTokenSource.Token.IsCancellationRequested)
					break;

				ProcessDiscoveredHost(response);
			}

			Console.WriteLine($"mDNS scan completed. Found {_discoveredServers.Count} servers");
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine("Server discovery scan cancelled");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error during server discovery: {ex.Message}");
			throw;
		}
		finally
		{
			_scanCancellationTokenSource?.Dispose();
			_scanCancellationTokenSource = null;
		}
	}

	/// <inheritdoc/>
	public async Task StopScanningAsync()
	{
		if (_scanCancellationTokenSource != null)
		{
			Console.WriteLine("Stopping server discovery scan");
			_scanCancellationTokenSource.Cancel();
			_scanCancellationTokenSource.Dispose();
			_scanCancellationTokenSource = null;
		}

		await Task.CompletedTask;
	}

	/// <inheritdoc/>
	public IReadOnlyList<DiscoveredServer> GetDiscoveredServers()
	{
		return _discoveredServers.Values.ToList();
	}

	private void ProcessDiscoveredHost(IZeroconfHost host)
	{
		try
		{
			Console.WriteLine($"Processing discovered host: {host.DisplayName}");

			foreach (var service in host.Services)
			{
				var server = ParseServer(host, service.Value);
				if (server != null)
				{
					var key = $"{server.IPAddress}:{server.Port}";

					if (_discoveredServers.TryAdd(key, server))
					{
						Console.WriteLine($"Discovered server: {server.DisplayName} at {server.Url}");
						ServerDiscovered?.Invoke(this, server);
					}
					else
					{
						Console.WriteLine($"Server already discovered: {key}");
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error processing discovered host '{host.DisplayName}': {ex.Message}");
		}
	}

	private DiscoveredServer? ParseServer(IZeroconfHost host, IService service)
	{
		try
		{
			// Get first IP address
			var ipAddress = host.IPAddress;
			if (string.IsNullOrEmpty(ipAddress))
			{
				Console.WriteLine($"No IP address found for host {host.DisplayName}");
				return null;
			}

			var port = service.Port;
			if (port == 0)
			{
				Console.WriteLine($"Invalid port for host {host.DisplayName}");
				return null;
			}

			var server = new DiscoveredServer
			{
				Hostname = host.DisplayName,
				IPAddress = ipAddress,
				Port = port,
				DiscoveredAt = DateTime.Now
			};

			// Parse TXT records for additional info
			if (service.Properties != null && service.Properties.Count > 0)
			{
				foreach (var property in service.Properties)
				{
					var key = property.Key.ToLowerInvariant();
					var value = property.Value;

					switch (key)
					{
						case "version":
							server.Version = value;
							break;
						case "ssl":
							server.UseSSL = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
							break;
						case "clients":
							if (int.TryParse(value, out var clients))
								server.ConnectedClients = clients;
							break;
					}
				}
			}

			return server;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error parsing server from host '{host.DisplayName}': {ex.Message}");
			return null;
		}
	}
}
