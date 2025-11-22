using System;

namespace DigitalSignage.Core.DTOs.Api;

/// <summary>
/// Data Transfer Object for device information in REST API
/// </summary>
public class DeviceDto
{
    /// <summary>
    /// Device unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Device hostname
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Device display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Device location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Device status (Online, Offline, Error)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Last seen timestamp
    /// </summary>
    public DateTime LastSeen { get; set; }

    /// <summary>
    /// Screen resolution
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// Currently assigned layout ID
    /// </summary>
    public int? CurrentLayoutId { get; set; }

    /// <summary>
    /// Currently assigned layout name
    /// </summary>
    public string? CurrentLayoutName { get; set; }

    /// <summary>
    /// Device IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Device operating system
    /// </summary>
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// CPU usage percentage (0-100)
    /// </summary>
    public double? CpuUsage { get; set; }

    /// <summary>
    /// Memory usage percentage (0-100)
    /// </summary>
    public double? MemoryUsage { get; set; }

    /// <summary>
    /// Disk usage percentage (0-100)
    /// </summary>
    public double? DiskUsage { get; set; }

    /// <summary>
    /// Device uptime in seconds
    /// </summary>
    public long? Uptime { get; set; }

    /// <summary>
    /// Device temperature in Celsius
    /// </summary>
    public double? Temperature { get; set; }
}

/// <summary>
/// Detailed device information including performance metrics
/// </summary>
public class DeviceDetailDto : DeviceDto
{
    /// <summary>
    /// Network interfaces
    /// </summary>
    public List<NetworkInterfaceDto> NetworkInterfaces { get; set; } = new();

    /// <summary>
    /// Mounted storage devices
    /// </summary>
    public List<StorageDeviceDto> StorageDevices { get; set; } = new();

    /// <summary>
    /// Recent device logs
    /// </summary>
    public List<DeviceLogDto> RecentLogs { get; set; } = new();
}

/// <summary>
/// Network interface information
/// </summary>
public class NetworkInterfaceDto
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// Storage device information
/// </summary>
public class StorageDeviceDto
{
    public string MountPoint { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes { get; set; }
    public double UsagePercent { get; set; }
}

/// <summary>
/// Device log entry
/// </summary>
public class DeviceLogDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
