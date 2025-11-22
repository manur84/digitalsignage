using System;

namespace DigitalSignage.Server.Models;

/// <summary>
/// Represents a message protocol version
/// Follows Semantic Versioning: MAJOR.MINOR.PATCH
/// </summary>
public class MessageVersion : IComparable<MessageVersion>, IEquatable<MessageVersion>
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }

    /// <summary>
    /// Current protocol version
    /// </summary>
    public static readonly MessageVersion Current = new(1, 0, 0);

    /// <summary>
    /// Minimum supported protocol version (for backward compatibility)
    /// </summary>
    public static readonly MessageVersion Minimum = new(1, 0, 0);

    public MessageVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Parse version from string (e.g., "1.0.0")
    /// </summary>
    public static MessageVersion Parse(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            throw new ArgumentException("Version string cannot be null or empty", nameof(versionString));

        var parts = versionString.Split('.');
        if (parts.Length != 3)
            throw new FormatException($"Invalid version format: {versionString}. Expected format: MAJOR.MINOR.PATCH");

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
        {
            throw new FormatException($"Invalid version format: {versionString}. Parts must be integers.");
        }

        return new MessageVersion(major, minor, patch);
    }

    /// <summary>
    /// Try parse version from string
    /// </summary>
    public static bool TryParse(string? versionString, out MessageVersion? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(versionString))
            return false;

        try
        {
            version = Parse(versionString);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if this version is compatible with the specified version
    /// Compatible = same major version, this minor >= other minor
    /// </summary>
    public bool IsCompatibleWith(MessageVersion other)
    {
        if (other == null)
            return false;

        // Breaking change: different major version
        if (Major != other.Major)
            return false;

        // Compatible: same major, this minor >= other minor
        // Server with version 1.2.0 can handle client with 1.0.0
        return Minor >= other.Minor;
    }

    /// <summary>
    /// Check if this version is within the supported range
    /// </summary>
    public bool IsSupported()
    {
        return this >= Minimum && this <= Current;
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public override bool Equals(object? obj) => Equals(obj as MessageVersion);

    public bool Equals(MessageVersion? other)
    {
        if (other == null)
            return false;

        return Major == other.Major &&
               Minor == other.Minor &&
               Patch == other.Patch;
    }

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);

    public int CompareTo(MessageVersion? other)
    {
        if (other == null)
            return 1;

        var majorCompare = Major.CompareTo(other.Major);
        if (majorCompare != 0)
            return majorCompare;

        var minorCompare = Minor.CompareTo(other.Minor);
        if (minorCompare != 0)
            return minorCompare;

        return Patch.CompareTo(other.Patch);
    }

    public static bool operator ==(MessageVersion? left, MessageVersion? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(MessageVersion? left, MessageVersion? right) => !(left == right);
    public static bool operator <(MessageVersion left, MessageVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(MessageVersion left, MessageVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(MessageVersion left, MessageVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MessageVersion left, MessageVersion right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Version compatibility result
/// </summary>
public class VersionCompatibilityResult
{
    public bool IsCompatible { get; set; }
    public string? Message { get; set; }
    public MessageVersion? ClientVersion { get; set; }
    public MessageVersion? ServerVersion { get; set; }

    public static VersionCompatibilityResult Compatible(MessageVersion clientVersion, MessageVersion serverVersion)
    {
        return new VersionCompatibilityResult
        {
            IsCompatible = true,
            Message = $"Client version {clientVersion} is compatible with server version {serverVersion}",
            ClientVersion = clientVersion,
            ServerVersion = serverVersion
        };
    }

    public static VersionCompatibilityResult Incompatible(MessageVersion clientVersion, MessageVersion serverVersion, string reason)
    {
        return new VersionCompatibilityResult
        {
            IsCompatible = false,
            Message = $"Client version {clientVersion} is incompatible with server version {serverVersion}: {reason}",
            ClientVersion = clientVersion,
            ServerVersion = serverVersion
        };
    }
}
