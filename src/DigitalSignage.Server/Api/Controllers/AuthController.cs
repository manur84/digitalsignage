using System;
using System.Threading.Tasks;
using DigitalSignage.Core.DTOs.Api;
using DigitalSignage.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Api.Controllers;

/// <summary>
/// Authentication controller for mobile app registration and token validation
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMobileAppService _mobileAppService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMobileAppService mobileAppService, ILogger<AuthController> logger)
    {
        _mobileAppService = mobileAppService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new mobile app
    /// </summary>
    /// <param name="request">Registration request with device information</param>
    /// <returns>Registration response with request ID</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterMobileAppResponse>> Register([FromBody] RegisterMobileAppRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.DeviceName))
            {
                return BadRequest(new RegisterMobileAppResponse
                {
                    Success = false,
                    Message = "Device name is required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Platform))
            {
                return BadRequest(new RegisterMobileAppResponse
                {
                    Success = false,
                    Message = "Platform is required"
                });
            }

            _logger.LogInformation(
                "Mobile app registration request: {DeviceName} ({Platform} {AppVersion})",
                request.DeviceName,
                request.Platform,
                request.AppVersion
            );

            // Create pending registration
            var result = await _mobileAppService.CreatePendingRegistrationAsync(
                request.DeviceName,
                request.Platform,
                request.AppVersion,
                request.DeviceModel,
                request.OsVersion
            );

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to create pending registration: {Error}", result.ErrorMessage);
                return BadRequest(new RegisterMobileAppResponse
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Failed to create registration request"
                });
            }

            var requestId = result.Value;

            return Ok(new RegisterMobileAppResponse
            {
                Success = true,
                Message = "Registration request submitted successfully. Please wait for approval on the server.",
                RequestId = requestId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing registration request");
            return StatusCode(500, new RegisterMobileAppResponse
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Check registration status
    /// </summary>
    /// <param name="requestId">Request ID from initial registration</param>
    /// <returns>Registration status (Pending, Approved, Denied) and token if approved</returns>
    [HttpGet("status/{requestId}")]
    [AllowAnonymous]
    public async Task<ActionResult<CheckRegistrationStatusResponse>> CheckStatus(Guid requestId)
    {
        try
        {
            _logger.LogDebug("Checking registration status for request {RequestId}", requestId);

            var result = await _mobileAppService.GetRegistrationStatusAsync(requestId);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get registration status for {RequestId}: {Error}", requestId, result.ErrorMessage);
                return NotFound(new CheckRegistrationStatusResponse
                {
                    Status = "NotFound",
                    Message = result.ErrorMessage ?? "Registration request not found"
                });
            }

            var status = result.Value;

            return Ok(new CheckRegistrationStatusResponse
            {
                Status = status.Status,
                Token = status.Token,
                MobileAppId = status.MobileAppId,
                Message = status.Status switch
                {
                    "Pending" => "Registration is pending approval",
                    "Approved" => "Registration approved",
                    "Denied" => "Registration denied",
                    _ => "Unknown status"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking registration status");
            return StatusCode(500, new CheckRegistrationStatusResponse
            {
                Status = "Error",
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Validate an authentication token
    /// </summary>
    /// <returns>Token validation result</returns>
    [HttpGet("validate")]
    [Authorize]
    public async Task<ActionResult<ValidateTokenResponse>> ValidateToken()
    {
        try
        {
            // If we reach here, authentication succeeded (handled by middleware)
            var appIdClaim = User.FindFirst("AppId")?.Value;

            if (string.IsNullOrEmpty(appIdClaim) || !Guid.TryParse(appIdClaim, out var appId))
            {
                return Unauthorized(new ValidateTokenResponse
                {
                    IsValid = false
                });
            }

            _logger.LogDebug("Token validated for mobile app {AppId}", appId);

            // Get mobile app details
            var appResult = await _mobileAppService.GetMobileAppAsync(appId);

            return Ok(new ValidateTokenResponse
            {
                IsValid = true,
                MobileAppId = appId,
                DeviceName = appResult.IsSuccess ? appResult.Value.DeviceName : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return StatusCode(500, new ValidateTokenResponse
            {
                IsValid = false
            });
        }
    }
}
