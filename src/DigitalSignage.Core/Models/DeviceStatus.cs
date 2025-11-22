namespace DigitalSignage.Core.Models;

/// <summary>
/// Device status enumeration
/// </summary>
public enum DeviceStatus
{
	/// <summary>
	/// Device is online and operational
	/// </summary>
	Online,

	/// <summary>
	/// Device is offline or not connected
	/// </summary>
	Offline,

	/// <summary>
	/// Device is online but has warnings or issues
	/// </summary>
	Warning,

	/// <summary>
	/// Device has encountered an error
	/// </summary>
	Error
}
