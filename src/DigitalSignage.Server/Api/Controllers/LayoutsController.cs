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

            var layoutsResult = await _layoutService.GetAllLayoutsAsync();

            if (!layoutsResult.IsSuccess)
            {
                _logger.LogWarning("Failed to get layouts: {Error}", layoutsResult.ErrorMessage);
                return StatusCode(500, layoutsResult.ErrorMessage ?? "Failed to get layouts");
            }

            var layoutDtos = layoutsResult.Value.Select(l => new LayoutDto
            {
                Id = int.TryParse(l.Id, out var layoutId) ? layoutId : 0,
                Name = l.Name ?? "Unnamed Layout",
                Description = l.Description,
                Width = l.Resolution?.Width ?? 1920,
                Height = l.Resolution?.Height ?? 1080,
                BackgroundColor = l.BackgroundColor,
                ElementCount = l.Elements?.Count ?? 0,
                CreatedAt = l.Created,
                ModifiedAt = l.Modified,
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

            var layoutResult = await _layoutService.GetLayoutByIdAsync(id.ToString());

            if (!layoutResult.IsSuccess || layoutResult.Value == null)
            {
                _logger.LogWarning("Layout {LayoutId} not found", id);
                return NotFound($"Layout {id} not found");
            }

            var layout = layoutResult.Value;

            var layoutDto = new LayoutDetailDto
            {
                Id = int.TryParse(layout.Id, out var layoutId) ? layoutId : 0,
                Name = layout.Name ?? "Unnamed Layout",
                Description = layout.Description,
                Width = layout.Resolution?.Width ?? 1920,
                Height = layout.Resolution?.Height ?? 1080,
                BackgroundColor = layout.BackgroundColor,
                ElementCount = layout.Elements?.Count ?? 0,
                CreatedAt = layout.Created,
                ModifiedAt = layout.Modified,
                ActiveDeviceCount = 0, // TODO: Calculate from client assignments
                Elements = new(),
                LayoutJson = includeElements ? System.Text.Json.JsonSerializer.Serialize(layout) : null
            };

            if (includeElements && layout.Elements != null)
            {
                layoutDto.Elements = layout.Elements.Select(e => new LayoutElementDto
                {
                    Id = Guid.TryParse(e.Id, out var elementId) ? elementId : Guid.Empty,
                    Type = e.Type ?? "Unknown",
                    X = (int)(e.Position?.X ?? 0),
                    Y = (int)(e.Position?.Y ?? 0),
                    Width = (int)(e.Size?.Width ?? 0),
                    Height = (int)(e.Size?.Height ?? 0),
                    Content = e.Properties?.GetValueOrDefault("Text")?.ToString() ??
                             e.Properties?.GetValueOrDefault("Content")?.ToString() ??
                             string.Empty
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
            var layoutResult = await _layoutService.GetLayoutByIdAsync(request.LayoutId.ToString());
            if (!layoutResult.IsSuccess || layoutResult.Value == null)
            {
                _logger.LogWarning("Layout {LayoutId} not found", request.LayoutId);
                return NotFound($"Layout {request.LayoutId} not found");
            }

            var layout = layoutResult.Value;

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
