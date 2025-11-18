namespace DigitalSignage.Data.Entities;

/// <summary>
/// Media file entity for central media library
/// </summary>
public class MediaFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MediaType Type { get; set; }
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? DurationSeconds { get; set; } // For videos
    public string? ThumbnailPath { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; } // Comma-separated tags
    public string? Category { get; set; }
    public int UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }
    public int AccessCount { get; set; } = 0;
    public bool IsPublic { get; set; } = true;
    public string? Hash { get; set; } // SHA256 hash for duplicate detection
}

public enum MediaType
{
    Image,
    Video,
    Audio,
    Document,
    Other
}
