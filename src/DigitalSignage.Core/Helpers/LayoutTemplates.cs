using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Helpers;

/// <summary>
/// Provides pre-defined layout templates
/// </summary>
public static class LayoutTemplates
{
    /// <summary>
    /// Helper method to create a DisplayElement and initialize default properties
    /// </summary>
    private static DisplayElement CreateElement(string type, string name, Position position, Size size, int zIndex, Dictionary<string, object> properties, string? dataBinding = null)
    {
        var element = new DisplayElement
        {
            Type = type,
            Name = name,
            Position = position,
            Size = size,
            ZIndex = zIndex,
            DataBinding = dataBinding,
            Properties = properties
        };

        // Initialize default properties to prevent KeyNotFoundException
        element.InitializeDefaultProperties();

        return element;
    }

    public static DisplayLayout CreateBlankLayout(string name = "Blank Layout")
    {
        return new DisplayLayout
        {
            Name = name,
            Resolution = new Resolution { Width = 1920, Height = 1080 },
            BackgroundColor = "#FFFFFF"
        };
    }

    public static DisplayLayout CreateRoomDisplayTemplate()
    {
        var layout = CreateBlankLayout("Room Display");
        layout.BackgroundColor = "#F0F4F8";

        // Room name header
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Room Name",
            position: new Position { X = 50, Y = 50 },
            size: new Size { Width = 1820, Height = 100 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{room.name}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 48,
                ["FontWeight"] = "bold",
                ["Color"] = "#1E3A8A",
                ["TextAlign"] = "center"
            },
            dataBinding: "{{room.name}}"
        ));

        // Status banner
        layout.Elements.Add(CreateElement(
            type: "shape",
            name: "Status Background",
            position: new Position { X = 50, Y = 200 },
            size: new Size { Width = 1820, Height = 150 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["ShapeType"] = "rectangle",
                ["FillColor"] = "#10B981",
                ["StrokeWidth"] = 0,
                ["CornerRadius"] = 8
            }
        ));

        // Status text
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Status",
            position: new Position { X = 50, Y = 200 },
            size: new Size { Width = 1820, Height = 150 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{room.status}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 56,
                ["FontWeight"] = "bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "center",
                ["VerticalAlign"] = "middle"
            },
            dataBinding: "{{room.status}}"
        ));

        // Current time
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Current Time",
            position: new Position { X = 50, Y = 950 },
            size: new Size { Width = 1820, Height = 80 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{current.time|HH:mm}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 48,
                ["Color"] = "#64748B",
                ["TextAlign"] = "center"
            }
        ));

        return layout;
    }

    public static DisplayLayout CreateWelcomeScreenTemplate()
    {
        var layout = CreateBlankLayout("Welcome Screen");
        layout.BackgroundColor = "#1E3A8A";

        // Welcome text
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Welcome",
            position: new Position { X = 100, Y = 300 },
            size: new Size { Width = 1720, Height = 150 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Welcome",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 96,
                ["FontWeight"] = "bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "center"
            }
        ));

        // Company/Location name
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Location",
            position: new Position { X = 100, Y = 500 },
            size: new Size { Width = 1720, Height = 100 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{company.name}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 48,
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "center"
            },
            dataBinding: "{{company.name}}"
        ));

        // QR Code
        layout.Elements.Add(CreateElement(
            type: "qrcode",
            name: "QR Code",
            position: new Position { X = 760, Y = 650 },
            size: new Size { Width = 400, Height = 400 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Data"] = "{{qr.url}}",
                ["ForegroundColor"] = "#000000",
                ["BackgroundColor"] = "#FFFFFF",
                ["ErrorCorrection"] = "M"
            },
            dataBinding: "{{qr.url}}"
        ));

        return layout;
    }

    public static DisplayLayout CreateInformationBoardTemplate()
    {
        var layout = CreateBlankLayout("Information Board");
        layout.BackgroundColor = "#FFFFFF";

        // Header
        layout.Elements.Add(CreateElement(
            type: "shape",
            name: "Header Background",
            position: new Position { X = 0, Y = 0 },
            size: new Size { Width = 1920, Height = 120 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["ShapeType"] = "rectangle",
                ["FillColor"] = "#2563EB",
                ["StrokeWidth"] = 0
            }
        ));

        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Header Text",
            position: new Position { X = 50, Y = 0 },
            size: new Size { Width = 1820, Height = 120 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Information Board",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 48,
                ["FontWeight"] = "bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "left",
                ["VerticalAlign"] = "middle"
            }
        ));

        // Main content area
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Content",
            position: new Position { X = 100, Y = 200 },
            size: new Size { Width = 1720, Height = 700 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{content.text}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 36,
                ["Color"] = "#1F2937",
                ["TextAlign"] = "left",
                ["WordWrap"] = true
            },
            dataBinding: "{{content.text}}"
        ));

        // Footer with date/time
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "DateTime",
            position: new Position { X = 100, Y = 960 },
            size: new Size { Width = 1720, Height = 80 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{current.datetime|dd.MM.yyyy HH:mm}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 32,
                ["Color"] = "#6B7280",
                ["TextAlign"] = "right"
            }
        ));

        return layout;
    }

    public static DisplayLayout CreateDirectoryTemplate()
    {
        var layout = CreateBlankLayout("Directory");
        layout.BackgroundColor = "#F9FAFB";

        // Title
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Title",
            position: new Position { X = 50, Y = 40 },
            size: new Size { Width = 1820, Height = 100 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Building Directory",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 64,
                ["FontWeight"] = "bold",
                ["Color"] = "#111827",
                ["TextAlign"] = "center"
            }
        ));

        // Directory table
        layout.Elements.Add(CreateElement(
            type: "table",
            name: "Directory Table",
            position: new Position { X = 100, Y = 180 },
            size: new Size { Width = 1720, Height = 820 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Columns"] = new[] { "Floor", "Department", "Room" },
                ["HeaderBackground"] = "#2563EB",
                ["RowBackground"] = "#FFFFFF",
                ["AlternateRowBackground"] = "#F3F4F6",
                ["BorderColor"] = "#D1D5DB",
                ["BorderWidth"] = 1
            },
            dataBinding: "{{directory.entries}}"
        ));

        return layout;
    }

    public static List<DisplayLayout> GetAllTemplates()
    {
        return new List<DisplayLayout>
        {
            CreateBlankLayout(),
            CreateRoomDisplayTemplate(),
            CreateWelcomeScreenTemplate(),
            CreateInformationBoardTemplate(),
            CreateDirectoryTemplate()
        };
    }
}
