using System.ComponentModel.DataAnnotations.Schema;

namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents a Raspberry Pi client device
/// </summary>
public class RaspberryPiClient
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Group { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public ClientStatus Status { get; set; } = ClientStatus.Offline;
    public DeviceInfo DeviceInfo { get; set; } = new();
    public string? AssignedLayoutId { get; set; }
    public List<Schedule> Schedules { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Navigation property for the assigned layout (not mapped to database)
    /// </summary>
    [NotMapped]
    public DisplayLayout? AssignedLayout { get; set; }

    /// <summary>
    /// Display name with fallbacks (Name -> Hostname -> mDNS -> IP)
    /// </summary>
    [NotMapped]
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name))
                return Name;

            if (DeviceInfo != null && !string.IsNullOrWhiteSpace(DeviceInfo.Hostname))
                return DeviceInfo.Hostname;

            if (DeviceInfo != null && !string.IsNullOrWhiteSpace(DeviceInfo.MdnsName))
                return DeviceInfo.MdnsName;

            // Try known metadata keys for mDNS service/host names
            if (Metadata != null)
            {
                if (Metadata.TryGetValue("MdnsName", out var mdns) && mdns != null && !string.IsNullOrWhiteSpace(mdns.ToString()))
                    return mdns.ToString()!;
                if (Metadata.TryGetValue("MdnsServiceName", out var mdnsService) && mdnsService != null && !string.IsNullOrWhiteSpace(mdnsService.ToString()))
                    return mdnsService.ToString()!;
            }

            return !string.IsNullOrWhiteSpace(IpAddress) ? IpAddress : "Unknown";
        }
    }

    /// <summary>
    /// Gets the assigned layout name for display purposes
    /// </summary>
    [NotMapped]
    public string AssignedLayoutName
    {
        get
        {
            if (string.IsNullOrEmpty(AssignedLayoutId) || AssignedLayoutId == Guid.Empty.ToString())
                return "Nicht zugewiesen";

            if (AssignedLayout != null)
                return AssignedLayout.Name;

            // Fallback: show shortened GUID if layout not loaded
            return AssignedLayoutId.Length > 8 ? AssignedLayoutId.Substring(0, 8) + "..." : AssignedLayoutId;
        }
    }
}

public enum ClientStatus
{
    Online,
    Offline,
    Error,
    Updating,
    Connecting,
    OfflineRecovery,
    Disconnected
}

/// <summary>
/// Device hardware and software information
/// </summary>
public class DeviceInfo
{
    public string MdnsName { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ClientVersion { get; set; } = string.Empty;
    public double CpuTemperature { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryTotal { get; set; }
    public long MemoryUsed { get; set; }
    public long DiskTotal { get; set; }
    public long DiskUsed { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public int NetworkLatency { get; set; } // milliseconds
    public long Uptime { get; set; } // seconds since boot
}

/// <summary>
/// Schedule for displaying different layouts at different times
/// </summary>
public class Schedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string LayoutId { get; set; } = string.Empty;
    public DayOfWeek[]? DaysOfWeek { get; set; } // null = all days
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Priority { get; set; } = 0; // Higher priority wins
    public bool Enabled { get; set; } = true;
}
