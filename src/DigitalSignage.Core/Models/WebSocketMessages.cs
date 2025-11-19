namespace DigitalSignage.Core.Models;

/// <summary>
/// WebSocket message for data source updates pushed to clients
/// </summary>
public class DataUpdateMessage : Message
{
    public DataUpdateMessage()
    {
        // ✅ CODE SMELL FIX: Use constants instead of magic strings
        Type = MessageTypes.DataUpdate;
    }

    public Guid DataSourceId { get; set; }
    public List<Dictionary<string, object>> Data { get; set; } = new();
}

/// <summary>
/// Information about a data source included when assigning a layout to a client
/// (Not a message itself, just data transfer object)
/// </summary>
public class LayoutDataSourceInfo
{
    public Guid DataSourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object>> InitialData { get; set; } = new();
}

/// <summary>
/// Extended layout assignment message that includes linked data sources
/// </summary>
public class LayoutAssignmentMessage : Message
{
    public LayoutAssignmentMessage()
    {
        // ✅ CODE SMELL FIX: Use constants instead of magic strings
        Type = MessageTypes.LayoutAssigned;
    }

    public string LayoutId { get; set; } = string.Empty;
    public DisplayLayout? Layout { get; set; }
    public List<LayoutDataSourceInfo> LinkedDataSources { get; set; } = new();
}
