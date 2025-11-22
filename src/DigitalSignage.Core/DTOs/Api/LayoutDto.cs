using System;

namespace DigitalSignage.Core.DTOs.Api;

/// <summary>
/// Data Transfer Object for layout information in REST API
/// </summary>
public class LayoutDto
{
    /// <summary>
    /// Layout unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Layout name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Layout description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Layout width in pixels
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Layout height in pixels
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Layout background color
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// Layout thumbnail image (base64 encoded)
    /// </summary>
    public string? ThumbnailBase64 { get; set; }

    /// <summary>
    /// Number of elements in the layout
    /// </summary>
    public int ElementCount { get; set; }

    /// <summary>
    /// Layout creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Layout last modified timestamp
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Number of devices currently displaying this layout
    /// </summary>
    public int ActiveDeviceCount { get; set; }
}

/// <summary>
/// Detailed layout information including elements
/// </summary>
public class LayoutDetailDto : LayoutDto
{
    /// <summary>
    /// Layout elements
    /// </summary>
    public List<LayoutElementDto> Elements { get; set; } = new();

    /// <summary>
    /// Full layout JSON definition
    /// </summary>
    public string? LayoutJson { get; set; }
}

/// <summary>
/// Layout element information
/// </summary>
public class LayoutElementDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string? Content { get; set; }
}

/// <summary>
/// Layout assignment request
/// </summary>
public class AssignLayoutRequest
{
    /// <summary>
    /// Layout ID to assign
    /// </summary>
    public int LayoutId { get; set; }

    /// <summary>
    /// Optional: Schedule start time (null = immediate)
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Optional: Schedule end time (null = indefinite)
    /// </summary>
    public DateTime? EndTime { get; set; }
}
