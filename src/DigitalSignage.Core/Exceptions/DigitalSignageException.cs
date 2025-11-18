namespace DigitalSignage.Core.Exceptions;

/// <summary>
/// Base exception for all Digital Signage application exceptions
/// </summary>
public abstract class DigitalSignageException : Exception
{
    protected DigitalSignageException(string message) : base(message)
    {
    }

    protected DigitalSignageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a service fails to initialize
/// </summary>
public class ServiceInitializationException : DigitalSignageException
{
    public string ServiceName { get; }

    public ServiceInitializationException(string serviceName, Exception innerException)
        : base($"Failed to initialize {serviceName}", innerException)
    {
        ServiceName = serviceName;
    }

    public ServiceInitializationException(string serviceName, string message, Exception innerException)
        : base($"Failed to initialize {serviceName}: {message}", innerException)
    {
        ServiceName = serviceName;
    }
}

/// <summary>
/// Exception thrown when a client is not found
/// </summary>
public class ClientNotFoundException : DigitalSignageException
{
    public string ClientId { get; }

    public ClientNotFoundException(string clientId)
        : base($"Client '{clientId}' not found")
    {
        ClientId = clientId;
    }
}

/// <summary>
/// Exception thrown when a layout is not found
/// </summary>
public class LayoutNotFoundException : DigitalSignageException
{
    public string LayoutId { get; }

    public LayoutNotFoundException(string layoutId)
        : base($"Layout '{layoutId}' not found")
    {
        LayoutId = layoutId;
    }
}

/// <summary>
/// Exception thrown when a data source is not found
/// </summary>
public class DataSourceNotFoundException : DigitalSignageException
{
    public Guid DataSourceId { get; }

    public DataSourceNotFoundException(Guid dataSourceId)
        : base($"Data source '{dataSourceId}' not found")
    {
        DataSourceId = dataSourceId;
    }
}

/// <summary>
/// Exception thrown when a media file is not found
/// </summary>
public class MediaNotFoundException : DigitalSignageException
{
    public string MediaId { get; }

    public MediaNotFoundException(string mediaId)
        : base($"Media '{mediaId}' not found")
    {
        MediaId = mediaId;
    }
}

/// <summary>
/// Exception thrown when input validation fails
/// </summary>
public class ValidationException : DigitalSignageException
{
    public string PropertyName { get; }

    public ValidationException(string propertyName, string message)
        : base($"Validation failed for '{propertyName}': {message}")
    {
        PropertyName = propertyName;
    }
}

/// <summary>
/// Exception thrown when a communication error occurs
/// </summary>
public class CommunicationException : DigitalSignageException
{
    public string? ClientId { get; }

    public CommunicationException(string message)
        : base($"Communication error: {message}")
    {
    }

    public CommunicationException(string clientId, string message, Exception innerException)
        : base($"Communication error with client '{clientId}': {message}", innerException)
    {
        ClientId = clientId;
    }
}

/// <summary>
/// Exception thrown when a database operation fails
/// </summary>
public class DatabaseException : DigitalSignageException
{
    public DatabaseException(string message, Exception innerException)
        : base($"Database error: {message}", innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a file operation fails
/// </summary>
public class FileOperationException : DigitalSignageException
{
    public string FilePath { get; }

    public FileOperationException(string filePath, string message, Exception innerException)
        : base($"File operation failed for '{filePath}': {message}", innerException)
    {
        FilePath = filePath;
    }
}
