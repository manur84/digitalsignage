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
/// Layouts controller for managing layouts and layout assignments
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LayoutsController : ControllerBase
{
    private readonly ILayoutService _layoutService;
    private readonly IClientService _clientService;
    private readonly ILogger<LayoutsController> _logger;

    public LayoutsController(
        ILayoutService layoutService,
        IClientService clientService,
        ILogger<LayoutsController> logger)
    {
        _layoutService = layoutService;
        _clientService = clientService;
        _logger = logger;
    }

    /// <summary>
    /// Get all layouts
    /// </summary>
    /// <returns>List of layouts</returns>
    [HttpGet]
    public async Task<ActionResult<List<LayoutDto>>> GetAllLayouts()
    {
        try
        {
            _logger.LogDebug("Getting all layouts");

            var layouts = await _layoutService.GetAllLayoutsAsync();

            var layoutDtos = layouts.Select(l => new LayoutDto
            {
                Id = l.Id,
                Name = l.Name ?? "Unnamed Layout",
                Description = l.Description,
                Width = l.Width,
                Height = l.Height,
                BackgroundColor = l.BackgroundColor,
                ElementCount = l.Elements?.Count ?? 0,
                CreatedAt = l.CreatedAt,
                ModifiedAt = l.ModifiedAt,
                ActiveDeviceCount = 0 // TODO: Calculate from client assignments
            }).ToList();

            _logger.LogDebug("Returning {Count} layouts", layoutDtos.Count);

            return Ok(layoutDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting layouts");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get layout by ID
    /// </summary>
    /// <param name="id">Layout ID</param>
    /// <param name="includeElements">Include layout elements (default: false)</param>
    /// <returns>Layout details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<LayoutDetailDto>> GetLayout(
        int id,
        [FromQuery] bool includeElements = false)
    {
        try
        {
            _logger.LogDebug("Getting layout {LayoutId} (includeElements: {IncludeElements})", id, includeElements);

            var layout = await _layoutService.GetLayoutAsync(id);

            if (layout == null)
            {
                _logger.LogWarning("Layout {LayoutId} not found", id);
                return NotFound($"Layout {id} not found");
            }

            var layoutDto = new LayoutDetailDto
            {
                Id = layout.Id,
                Name = layout.Name ?? "Unnamed Layout",
                Description = layout.Description,
                Width = layout.Width,
                Height = layout.Height,
                BackgroundColor = layout.BackgroundColor,
                ElementCount = layout.Elements?.Count ?? 0,
                CreatedAt = layout.CreatedAt,
                ModifiedAt = layout.ModifiedAt,
                ActiveDeviceCount = 0, // TODO: Calculate from client assignments
                Elements = new(),
                LayoutJson = includeElements ? layout.LayoutJson : null
            };

            if (includeElements && layout.Elements != null)
            {
                layoutDto.Elements = layout.Elements.Select(e => new LayoutElementDto
                {
                    Id = e.Id,
                    Type = e.Type ?? "Unknown",
                    X = e.X,
                    Y = e.Y,
                    Width = e.Width,
                    Height = e.Height,
                    Content = e.Content
                }).ToList();
            }

            return Ok(layoutDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting layout {LayoutId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Assign layout to device
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="request">Layout assignment request</param>
    /// <returns>Assignment result</returns>
    [HttpPost("assign/{deviceId}")]
    public async Task<ActionResult> AssignLayoutToDevice(
        Guid deviceId,
        [FromBody] AssignLayoutRequest request)
    {
        try
        {
            if (request.LayoutId <= 0)
            {
                return BadRequest("Invalid layout ID");
            }

            _logger.LogInformation(
                "Assigning layout {LayoutId} to device {DeviceId}",
                request.LayoutId,
                deviceId
            );

            // Validate device exists
            var clientResult = await _clientService.GetClientByIdAsync(deviceId.ToString());
            if (!clientResult.IsSuccess)
            {
                _logger.LogWarning("Device {DeviceId} not found", deviceId);
                return NotFound($"Device {deviceId} not found");
            }

            var client = clientResult.Value;

            // Validate layout exists
            var layout = await _layoutService.GetLayoutAsync(request.LayoutId);
            if (layout == null)
            {
                _logger.LogWarning("Layout {LayoutId} not found", request.LayoutId);
                return NotFound($"Layout {request.LayoutId} not found");
            }

            // Assign layout to device
            var result = await _clientService.AssignLayoutAsync(deviceId.ToString(), request.LayoutId.ToString());

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to assign layout {LayoutId} to device {DeviceId}: {Error}",
                    request.LayoutId,
                    deviceId,
                    result.ErrorMessage
                );

                return BadRequest(new
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Failed to assign layout"
                });
            }

            _logger.LogInformation(
                "Successfully assigned layout {LayoutId} to device {DeviceId}",
                request.LayoutId,
                deviceId
            );

            return Ok(new
            {
                Success = true,
                Message = $"Layout '{layout.Name}' assigned to device '{client.Name ?? client.DeviceInfo?.Hostname ?? "Unknown"}'",
                LayoutId = request.LayoutId,
                DeviceId = deviceId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error assigning layout {LayoutId} to device {DeviceId}",
                request.LayoutId,
                deviceId
            );
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Remove layout assignment from device
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Removal result</returns>
    [HttpDelete("assign/{deviceId}")]
    public async Task<ActionResult> RemoveLayoutFromDevice(Guid deviceId)
    {
        try
        {
            _logger.LogInformation("Removing layout assignment from device {DeviceId}", deviceId);

            // Validate device exists
            var clientResult = await _clientService.GetClientByIdAsync(deviceId.ToString());
            if (!clientResult.IsSuccess)
            {
                _logger.LogWarning("Device {DeviceId} not found", deviceId);
                return NotFound($"Device {deviceId} not found");
            }

            var client = clientResult.Value;

            // Remove layout assignment (assign empty layout ID)
            var result = await _clientService.AssignLayoutAsync(deviceId.ToString(), "");

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to remove layout from device {DeviceId}: {Error}",
                    deviceId,
                    result.ErrorMessage
                );

                return BadRequest(new
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Failed to remove layout"
                });
            }

            _logger.LogInformation("Successfully removed layout from device {DeviceId}", deviceId);

            return Ok(new
            {
                Success = true,
                Message = $"Layout removed from device '{client.Name ?? client.DeviceInfo?.Hostname ?? "Unknown"}'",
                DeviceId = deviceId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing layout from device {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }
}
