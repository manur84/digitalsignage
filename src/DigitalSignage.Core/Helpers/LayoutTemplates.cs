using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Helpers;

/// <summary>
/// Provides pre-defined layout templates
/// </summary>
public static class LayoutTemplates
{
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
        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "Room Name",
            Position = new Position { X = 50, Y = 50 },
            Size = new Size { Width = 1820, Height = 100 },
            ZIndex = 1,
            DataBinding = "{{room.name}}",
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "{{room.name}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 48,
                ["FontWeight"] = "bold",
                ["Color"] = "#1E3A8A",
                ["TextAlign"] = "center"
            }
        });

        // Status banner
        layout.Elements.Add(new DisplayElement
        {
            Type = "shape",
            Name = "Status Background",
            Position = new Position { X = 50, Y = 200 },
            Size = new Size { Width = 1820, Height = 150 },
            ZIndex = 0,
            Properties = new Dictionary<string, object>
            {
                ["ShapeType"] = "rectangle",
                ["FillColor"] = "#10B981",
                ["StrokeWidth"] = 0,
                ["CornerRadius"] = 8
            }
        });

        // Status text
        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "Status",
            Position = new Position { X = 50, Y = 200 },
            Size = new Size { Width = 1820, Height = 150 },
            ZIndex = 1,
            DataBinding = "{{room.status}}",
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "{{room.status}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 56,
                ["FontWeight"] = "bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "center",
                ["VerticalAlign"] = "middle"
            }
        });

        // Current time
        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "Current Time",
            Position = new Position { X = 50, Y = 950 },
            Size = new Size { Width = 1820, Height = 80 },
            ZIndex = 1,
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "{{current.time|HH:mm}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 48,
                ["Color"] = "#64748B",
                ["TextAlign"] = "center"
            }
        });

        return layout;
    }

    public static DisplayLayout CreateWelcomeScreenTemplate()
    {
        var layout = CreateBlankLayout("Welcome Screen");
        layout.BackgroundColor = "#1E3A8A";

        // Welcome text
        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "Welcome",
            Position = new Position { X = 100, Y = 300 },
            Size = new Size { Width = 1720, Height = 150 },
            ZIndex = 1,
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "Welcome",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 96,
                ["FontWeight"] = "bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "center"
            }
        });

        // Company/Location name
        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "Location",
            Position = new Position { X = 100, Y = 500 },
            Size = new Size { Width = 1720, Height = 100 },
            ZIndex = 1,
            DataBinding = "{{company.name}}",
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "{{company.name}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 48,
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "center"
            }
        });

        // QR Code
        layout.Elements.Add(new DisplayElement
        {
            Type = "qrcode",
            Name = "QR Code",
            Position = new Position { X = 760, Y = 650 },
            Size = new Size { Width = 400, Height = 400 },
            ZIndex = 1,
            DataBinding = "{{qr.url}}",
            Properties = new Dictionary<string, object>
            {
                ["Data"] = "{{qr.url}}",
                ["ForegroundColor"] = "#000000",
                ["BackgroundColor"] = "#FFFFFF",
                ["ErrorCorrection"] = "M"
            }
        });

        return layout;
    }

    public static DisplayLayout CreateInformationBoardTemplate()
    {
        var layout = CreateBlankLayout("Information Board");
        layout.BackgroundColor = "#FFFFFF";

        // Header
        layout.Elements.Add(new DisplayElement
        {
            Type = "shape",
            Name = "Header Background",
            Position = new Position { X = 0, Y = 0 },
            Size = new Size { Width = 1920, Height = 120 },
            ZIndex = 0,
            Properties = new Dictionary<string, object>
            {
                ["ShapeType"] = "rectangle",
                ["FillColor"] = "#2563EB",
                ["StrokeWidth"] = 0
            }
        });

        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "Header Text",
            Position = new Position { X = 50, Y = 0 },
            Size = new Size { Width = 1820, Height = 120 },
            ZIndex = 1,
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "Information Board",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 48,
                ["FontWeight"] = "bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "left",
                ["VerticalAlign"] = "middle"
            }
        });

        // Main content area
        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "Content",
            Position = new Position { X = 100, Y = 200 },
            Size = new Size { Width = 1720, Height = 700 },
            ZIndex = 1,
            DataBinding = "{{content.text}}",
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "{{content.text}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 36,
                ["Color"] = "#1F2937",
                ["TextAlign"] = "left",
                ["WordWrap"] = true
            }
        });

        // Footer with date/time
        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "DateTime",
            Position = new Position { X = 100, Y = 960 },
            Size = new Size { Width = 1720, Height = 80 },
            ZIndex = 1,
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "{{current.datetime|dd.MM.yyyy HH:mm}}",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 32,
                ["Color"] = "#6B7280",
                ["TextAlign"] = "right"
            }
        });

        return layout;
    }

    public static DisplayLayout CreateDirectoryTemplate()
    {
        var layout = CreateBlankLayout("Directory");
        layout.BackgroundColor = "#F9FAFB";

        // Title
        layout.Elements.Add(new DisplayElement
        {
            Type = "text",
            Name = "Title",
            Position = new Position { X = 50, Y = 40 },
            Size = new Size { Width = 1820, Height = 100 },
            ZIndex = 1,
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "Building Directory",
                ["FontFamily"] = "Segoe UI",
                ["FontSize"] = 64,
                ["FontWeight"] = "bold",
                ["Color"] = "#111827",
                ["TextAlign"] = "center"
            }
        });

        // Directory table
        layout.Elements.Add(new DisplayElement
        {
            Type = "table",
            Name = "Directory Table",
            Position = new Position { X = 100, Y = 180 },
            Size = new Size { Width = 1720, Height = 820 },
            ZIndex = 1,
            DataBinding = "{{directory.entries}}",
            Properties = new Dictionary<string, object>
            {
                ["Columns"] = new[] { "Floor", "Department", "Room" },
                ["HeaderBackground"] = "#2563EB",
                ["RowBackground"] = "#FFFFFF",
                ["AlternateRowBackground"] = "#F3F4F6",
                ["BorderColor"] = "#D1D5DB",
                ["BorderWidth"] = 1
            }
        });

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
