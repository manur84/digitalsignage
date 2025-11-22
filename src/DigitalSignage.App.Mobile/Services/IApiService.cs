using DigitalSignage.Core.DTOs.Api;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Service for communicating with the Digital Signage REST API.
/// Provides an alternative to WebSocket communication for mobile apps.
/// </summary>
public interface IApiService
{
	/// <summary>
	/// Registers a new mobile app with the server.
	/// Returns a request ID that can be used to poll for approval status.
	/// </summary>
	/// <param name="deviceName">The mobile device name</param>
	/// <param name="platform">The platform (iOS, Android)</param>
	/// <param name="appVersion">The app version</param>
	/// <param name="deviceModel">The device model</param>
	/// <param name="osVersion">The OS version</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Registration response with request ID</returns>
	Task<RegisterMobileAppResponse> RegisterAsync(
		string deviceName,
		string platform,
		string appVersion,
		string? deviceModel = null,
		string? osVersion = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks the status of a mobile app registration request.
	/// Poll this endpoint until status is "Approved" or "Denied".
	/// </summary>
	/// <param name="requestId">The request ID from initial registration</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Registration status response with token if approved</returns>
	Task<CheckRegistrationStatusResponse> CheckRegistrationStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a list of all devices, optionally filtered by status.
	/// </summary>
	/// <param name="status">Optional status filter (Online, Offline, Error)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of device DTOs</returns>
	Task<List<DeviceDto>> GetDevicesAsync(
		string? status = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets detailed information about a specific device by ID.
	/// </summary>
	/// <param name="deviceId">The device ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Device detail DTO</returns>
	Task<DeviceDetailDto> GetDeviceByIdAsync(
		Guid deviceId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Sends a command to a specific device.
	/// </summary>
	/// <param name="deviceId">The device ID</param>
	/// <param name="command">The command to send (Restart, Screenshot, VolumeUp, etc.)</param>
	/// <param name="parameters">Optional command parameters</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Command response</returns>
	Task<DeviceCommandResponse> SendCommandAsync(
		Guid deviceId,
		string command,
		Dictionary<string, string>? parameters = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Requests a screenshot from a specific device.
	/// </summary>
	/// <param name="deviceId">The device ID</param>
	/// <param name="quality">Screenshot quality (1-100)</param>
	/// <param name="timeoutSeconds">Timeout in seconds</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Screenshot response with base64 encoded image</returns>
	Task<ScreenshotResponse> RequestScreenshotAsync(
		Guid deviceId,
		int quality = 85,
		int timeoutSeconds = 30,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a list of all available layouts.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of layout DTOs</returns>
	Task<List<LayoutDto>> GetLayoutsAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets detailed information about a specific layout by ID.
	/// </summary>
	/// <param name="layoutId">The layout ID</param>
	/// <param name="includeElements">Whether to include layout elements</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Layout detail DTO</returns>
	Task<LayoutDetailDto> GetLayoutByIdAsync(
		int layoutId,
		bool includeElements = true,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Assigns a layout to a specific device.
	/// </summary>
	/// <param name="deviceId">The device ID</param>
	/// <param name="layoutId">The layout ID to assign</param>
	/// <param name="startTime">Optional start time</param>
	/// <param name="endTime">Optional end time</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if assignment was successful</returns>
	Task<bool> AssignLayoutToDeviceAsync(
		Guid deviceId,
		int layoutId,
		DateTime? startTime = null,
		DateTime? endTime = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets server information and health status.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Server info response</returns>
	Task<ServerInfoResponse> GetServerInfoAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Sets the authentication token for API requests.
	/// </summary>
	/// <param name="token">The bearer token</param>
	void SetAuthenticationToken(string token);

	/// <summary>
	/// Sets the server base URL for API requests.
	/// </summary>
	/// <param name="baseUrl">The server base URL (e.g., "https://192.168.1.100:5000")</param>
	void SetServerUrl(string baseUrl);
}
