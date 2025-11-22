using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using DigitalSignage.Core.DTOs.Api;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Api.Controllers;

/// <summary>
/// Server information controller (no authentication required for basic info)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ServerInfoController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly ILayoutService _layoutService;
    private readonly IMobileAppService _mobileAppService;
    private readonly ICommunicationService? _webSocketService;
    private readonly ILogger<ServerInfoController> _logger;
    private static readonly DateTime _serverStartTime = DateTime.UtcNow;

    public ServerInfoController(
        IClientService clientService,
        ILayoutService layoutService,
        IMobileAppService mobileAppService,
        ILogger<ServerInfoController> logger,
        ICommunicationService? webSocketService = null)
    {
        _clientService = clientService;
        _layoutService = layoutService;
        _mobileAppService = mobileAppService;
        _webSocketService = webSocketService;
        _logger = logger;
    }

    /// <summary>
    /// Get server information and status
    /// </summary>
    /// <returns>Server info</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ServerInfoResponse>> GetServerInfo()
    {
        try
        {
            _logger.LogDebug("Getting server info");

            // Get version from assembly
            var version = Assembly.GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString() ?? "1.0.0";

            // Get counts
            var clientsResult = await _clientService.GetAllClientsAsync();
            var clients = clientsResult.IsSuccess ? clientsResult.Value : new List<Core.Models.RaspberryPiClient>();
            var connectedClientCount = clients.Count(c => c.Status == Core.Models.ClientStatus.Online);
            var registeredMobileAppCount = await _mobileAppService.GetAllRegistrationsAsync();

            var layouts = await _layoutService.GetAllLayoutsAsync();

            // Get WebSocket status (simplified - no IsRunning/Port properties on ICommunicationService)
            var webSocketStatus = _webSocketService != null ? "Running" : "Not Available";
            string? webSocketUrl = _webSocketService != null ? "ws://localhost:8080" : null;

            // Calculate uptime
            var uptime = (long)(DateTime.UtcNow - _serverStartTime).TotalSeconds;

            var serverInfo = new ServerInfoResponse
            {
                Version = version,
                ServerName = Environment.MachineName,
                Status = "Running",
                ConnectedClientCount = connectedClientCount,
                RegisteredMobileAppCount = registeredMobileAppCount.Count,
                TotalLayoutCount = layouts.Count,
                WebSocketStatus = webSocketStatus,
                WebSocketUrl = webSocketUrl,
                UptimeSeconds = uptime
            };

            return Ok(serverInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting server info");
            return StatusCode(500, new ServerInfoResponse
            {
                Version = "Unknown",
                ServerName = "Unknown",
                Status = "Error",
                ConnectedClientCount = 0,
                RegisteredMobileAppCount = 0,
                TotalLayoutCount = 0,
                WebSocketStatus = "Unknown",
                UptimeSeconds = 0
            });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [AllowAnonymous]
    public ActionResult<object> GetHealth()
    {
        try
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Uptime = (DateTime.UtcNow - _serverStartTime).TotalSeconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return StatusCode(500, new
            {
                Status = "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get detailed server statistics (requires authentication)
    /// </summary>
    /// <returns>Server statistics</returns>
    [HttpGet("statistics")]
    [Authorize]
    public async Task<ActionResult<object>> GetStatistics()
    {
        try
        {
            _logger.LogDebug("Getting server statistics");

            var clientsResult = await _clientService.GetAllClientsAsync();
            var clients = clientsResult.IsSuccess ? clientsResult.Value : new List<Core.Models.RaspberryPiClient>();
            var layouts = await _layoutService.GetAllLayoutsAsync();
            var mobileApps = await _mobileAppService.GetAllRegistrationsAsync();

            // Get process information
            var process = Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64 / 1024.0 / 1024.0; // MB
            var cpuTime = process.TotalProcessorTime.TotalSeconds;

            var statistics = new
            {
                Server = new
                {
                    Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                    Platform = Environment.OSVersion.Platform.ToString(),
                    OsVersion = Environment.OSVersion.VersionString,
                    ProcessorCount = Environment.ProcessorCount,
                    MachineName = Environment.MachineName,
                    UptimeSeconds = (DateTime.UtcNow - _serverStartTime).TotalSeconds
                },
                Process = new
                {
                    MemoryUsageMB = Math.Round(memoryUsage, 2),
                    CpuTimeSeconds = Math.Round(cpuTime, 2),
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount
                },
                Clients = new
                {
                    Total = clients.Count,
                    Online = clients.Count(c => c.Status == Core.Models.ClientStatus.Online),
                    Offline = clients.Count(c => c.Status != Core.Models.ClientStatus.Online),
                    ByStatus = clients.GroupBy(c => c.Status).Select(g => new
                    {
                        Status = g.Key.ToString(),
                        Count = g.Count()
                    })
                },
                Layouts = new
                {
                    Total = layouts.Count,
                    AverageElementCount = layouts.Any()
                        ? Math.Round(layouts.Average(l => l.Elements?.Count ?? 0), 1)
                        : 0
                },
                MobileApps = new
                {
                    Total = mobileApps.Count,
                    Pending = mobileApps.Count(a => a.Status == Core.Models.AppRegistrationStatus.Pending),
                    Approved = mobileApps.Count(a => a.Status == Core.Models.AppRegistrationStatus.Approved),
                    Rejected = mobileApps.Count(a => a.Status == Core.Models.AppRegistrationStatus.Rejected),
                    Revoked = mobileApps.Count(a => a.Status == Core.Models.AppRegistrationStatus.Revoked)
                }
            };

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting server statistics");
            return StatusCode(500, "Internal server error");
        }
    }
}
