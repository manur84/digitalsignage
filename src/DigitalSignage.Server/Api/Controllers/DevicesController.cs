using System;
using System.Linq;
using System.Threading.Tasks;
using DigitalSignage.Core.DTOs.Api;
using DigitalSignage.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Api.Controllers;

/// <summary>
/// Devices controller for managing client devices (simplified version)
/// </summary>
[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IClientService clientService,
        ILogger<DevicesController> logger)
    {
        _clientService = clientService;
        _logger = logger;
    }

    /// <summary>
    /// Get all devices
    /// </summary>
    /// <param name="status">Optional status filter (Online, Offline, Error)</param>
    /// <returns>List of devices</returns>
    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> GetAllDevices([FromQuery] string? status = null)
    {
        try
        {
            _logger.LogDebug("Getting all devices (status filter: {Status})", status ?? "none");

            var result = await _clientService.GetAllClientsAsync();

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get clients: {Error}", result.ErrorMessage);
                return StatusCode(500, result.ErrorMessage ?? "Failed to get devices");
            }

            var clients = result.Value;

            // Apply status filter if provided
            if (!string.IsNullOrWhiteSpace(status))
            {
                clients = clients.Where(c =>
                    c.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var devices = clients.Select(c => new DeviceDto
            {
                Id = Guid.Parse(c.Id),
                Hostname = c.DeviceInfo?.Hostname ?? c.Name ?? string.Empty,
                Name = c.Name ?? c.DeviceInfo?.Hostname ?? "Unknown",
                Location = c.Location,
                Status = c.Status.ToString(),
                LastSeen = c.LastSeen,
                Resolution = c.DeviceInfo != null ? $"{c.DeviceInfo.ScreenWidth}x{c.DeviceInfo.ScreenHeight}" : null,
                CurrentLayoutId = !string.IsNullOrEmpty(c.AssignedLayoutId) ? Guid.Parse(c.AssignedLayoutId) : null,
                CurrentLayoutName = c.AssignedLayout?.Name,
                IpAddress = c.IpAddress,
                OperatingSystem = c.DeviceInfo?.OsVersion,
                CpuUsage = c.DeviceInfo?.CpuUsage,
                MemoryUsage = c.DeviceInfo?.MemoryTotal > 0 ? (double)c.DeviceInfo.MemoryUsed / c.DeviceInfo.MemoryTotal * 100 : null,
                DiskUsage = c.DeviceInfo?.DiskTotal > 0 ? (double)c.DeviceInfo.DiskUsed / c.DeviceInfo.DiskTotal * 100 : null,
                Uptime = c.DeviceInfo?.Uptime,
                Temperature = c.DeviceInfo?.CpuTemperature
            }).ToList();

            _logger.LogDebug("Returning {Count} devices", devices.Count);

            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting devices");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get device by ID
    /// </summary>
    /// <param name="id">Device ID</param>
    /// <returns>Device details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<DeviceDto>> GetDevice(Guid id)
    {
        try
        {
            _logger.LogDebug("Getting device {DeviceId}", id);

            var result = await _clientService.GetClientByIdAsync(id.ToString());

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Device {DeviceId} not found: {Error}", id, result.ErrorMessage);
                return NotFound($"Device {id} not found");
            }

            var client = result.Value;

            var device = new DeviceDto
            {
                Id = Guid.Parse(client.Id),
                Hostname = client.DeviceInfo?.Hostname ?? client.Name ?? string.Empty,
                Name = client.Name ?? client.DeviceInfo?.Hostname ?? "Unknown",
                Location = client.Location,
                Status = client.Status.ToString(),
                LastSeen = client.LastSeen,
                Resolution = client.DeviceInfo != null ? $"{client.DeviceInfo.ScreenWidth}x{client.DeviceInfo.ScreenHeight}" : null,
                CurrentLayoutId = !string.IsNullOrEmpty(client.AssignedLayoutId) ? Guid.Parse(client.AssignedLayoutId) : null,
                CurrentLayoutName = client.AssignedLayout?.Name,
                IpAddress = client.IpAddress,
                OperatingSystem = client.DeviceInfo?.OsVersion,
                CpuUsage = client.DeviceInfo?.CpuUsage,
                MemoryUsage = client.DeviceInfo?.MemoryTotal > 0 ? (double)client.DeviceInfo.MemoryUsed / client.DeviceInfo.MemoryTotal * 100 : null,
                DiskUsage = client.DeviceInfo?.DiskTotal > 0 ? (double)client.DeviceInfo.DiskUsed / client.DeviceInfo.DiskTotal * 100 : null,
                Uptime = client.DeviceInfo?.Uptime,
                Temperature = client.DeviceInfo?.CpuTemperature
            };

            return Ok(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device {DeviceId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Send a command to a device
    /// </summary>
    /// <param name="id">Device ID</param>
    /// <param name="request">Command request</param>
    /// <returns>Command response</returns>
    [HttpPost("{id}/command")]
    public async Task<ActionResult<DeviceCommandResponse>> SendCommand(
        Guid id,
        [FromBody] DeviceCommandRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Command))
            {
                return BadRequest(new DeviceCommandResponse
                {
                    Success = false,
                    Message = "Command is required",
                    Timestamp = DateTime.UtcNow
                });
            }

            _logger.LogInformation(
                "Sending command {Command} to device {DeviceId}",
                request.Command,
                id
            );

            // Send command via ClientService
            var result = await _clientService.SendCommandAsync(
                id.ToString(),
                request.Command,
                request.Parameters
            );

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Command {Command} failed for device {DeviceId}: {Error}",
                    request.Command,
                    id,
                    result.ErrorMessage
                );

                return BadRequest(new DeviceCommandResponse
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Command failed",
                    Timestamp = DateTime.UtcNow
                });
            }

            return Ok(new DeviceCommandResponse
            {
                Success = true,
                Message = $"Command {request.Command} sent successfully",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command to device {DeviceId}", id);
            return StatusCode(500, new DeviceCommandResponse
            {
                Success = false,
                Message = "Internal server error",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get device status (real-time)
    /// </summary>
    /// <param name="id">Device ID</param>
    /// <returns>Device status information</returns>
    [HttpGet("{id}/status")]
    public async Task<ActionResult<object>> GetDeviceStatus(Guid id)
    {
        try
        {
            _logger.LogDebug("Getting status for device {DeviceId}", id);

            var result = await _clientService.GetClientByIdAsync(id.ToString());

            if (!result.IsSuccess)
            {
                return NotFound($"Device {id} not found");
            }

            var client = result.Value;

            var status = new
            {
                DeviceId = client.Id,
                Status = client.Status.ToString(),
                IsOnline = client.Status == Core.Models.ClientStatus.Online,
                LastSeen = client.LastSeen,
                CurrentLayout = new
                {
                    Name = client.AssignedLayout?.Name
                },
                Performance = new
                {
                    CpuUsage = client.DeviceInfo?.CpuUsage,
                    MemoryUsage = client.DeviceInfo?.MemoryTotal > 0 ? (double)client.DeviceInfo.MemoryUsed / client.DeviceInfo.MemoryTotal * 100 : (double?)null,
                    DiskUsage = client.DeviceInfo?.DiskTotal > 0 ? (double)client.DeviceInfo.DiskUsed / client.DeviceInfo.DiskTotal * 100 : (double?)null,
                    Temperature = client.DeviceInfo?.CpuTemperature,
                    Uptime = client.DeviceInfo?.Uptime
                }
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status for device {DeviceId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
