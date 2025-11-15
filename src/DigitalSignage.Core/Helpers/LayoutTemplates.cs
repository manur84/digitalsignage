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

    public static DisplayLayout CreateParkingOverviewTemplate()
    {
        var layout = CreateBlankLayout("Parkplatz-Übersicht (IHK)");
        layout.Resolution = new Resolution { Width = 1920, Height = 1080 };
        layout.BackgroundColor = "#FFFFFF";

        // Header with IHK branding
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Header Background",
            position: new Position { X = 0, Y = 0 },
            size: new Size { Width = 1920, Height = 120 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#003366",  // IHK Primary
                ["BorderThickness"] = 0.0,
                ["CornerRadius"] = 0.0
            }
        ));

        // Header bottom accent
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Header Accent",
            position: new Position { X = 0, Y = 112 },
            size: new Size { Width = 1920, Height = 8 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#56bd66",  // IHK Secondary
                ["BorderThickness"] = 0.0
            }
        ));

        // Title
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Title",
            position: new Position { X = 60, Y = 20 },
            size: new Size { Width = 1800, Height = 80 },
            zIndex: 2,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Parkplatz Verwaltung",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 48.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            }
        ));

        // Statistics Panel
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Stats Background",
            position: new Position { X = 40, Y = 150 },
            size: new Size { Width = 1840, Height = 140 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#e3ebf5",  // IHK Primary Dimmed 04
                ["BorderColor"] = "#56bd66",
                ["BorderThickness"] = 0.0,
                ["CornerRadius"] = 0.0
            }
        ));

        // Stat 1: Gesamt
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Total Number",
            position: new Position { X = 100, Y = 170 },
            size: new Size { Width = 350, Height = 50 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{stats.total}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 42.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            },
            dataBinding: "{{stats.total}}"
        ));

        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Total Label",
            position: new Position { X = 100, Y = 230 },
            size: new Size { Width = 350, Height = 40 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Gesamt",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 18.0,
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            }
        ));

        // Stat 2: Belegt
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Occupied Number",
            position: new Position { X = 520, Y = 170 },
            size: new Size { Width = 350, Height = 50 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{stats.occupied}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 42.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#ea515a",  // IHK Error
                ["TextAlign"] = "Center"
            },
            dataBinding: "{{stats.occupied}}"
        ));

        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Occupied Label",
            position: new Position { X = 520, Y = 230 },
            size: new Size { Width = 350, Height = 40 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Belegt",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 18.0,
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            }
        ));

        // Stat 3: Frei
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Free Number",
            position: new Position { X = 940, Y = 170 },
            size: new Size { Width = 350, Height = 50 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{stats.free}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 42.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#56bd66",  // IHK Success
                ["TextAlign"] = "Center"
            },
            dataBinding: "{{stats.free}}"
        ));

        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Free Label",
            position: new Position { X = 940, Y = 230 },
            size: new Size { Width = 350, Height = 40 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Frei",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 18.0,
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            }
        ));

        // Stat 4: Auslastung
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Usage Number",
            position: new Position { X = 1360, Y = 170 },
            size: new Size { Width = 350, Height = 50 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{stats.usage_percent}}%",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 42.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            },
            dataBinding: "{{stats.usage_percent}}"
        ));

        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Usage Label",
            position: new Position { X = 1360, Y = 230 },
            size: new Size { Width = 350, Height = 40 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Auslastung",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 18.0,
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            }
        ));

        // Content area instruction
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Content Info",
            position: new Position { X = 100, Y = 320 },
            size: new Size { Width = 1720, Height = 700 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Parkplatz-Übersicht\n\nDieses Layout zeigt alle Parkplätze über alle Standorte hinweg.\n\nVerbinden Sie eine Datenquelle mit folgenden Feldern:\n- stats.total, stats.occupied, stats.free, stats.usage_percent\n- locations[] Array mit Standorten\n\nHinweis: Für dynamische Parkplätze nutzen Sie die Scriban-Template-Engine.",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 24.0,
                ["Color"] = "#335c85",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle",
                ["WordWrap"] = true
            }
        ));

        // Footer
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Footer",
            position: new Position { X = 60, Y = 1020 },
            size: new Size { Width = 1800, Height = 40 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Stand: {{current.datetime|dd.MM.yyyy HH:mm}} Uhr",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 18.0,
                ["Color"] = "#335c85",
                ["TextAlign"] = "Center"
            }
        ));

        return layout;
    }

    public static DisplayLayout CreateParkingRoomTemplate()
    {
        var layout = CreateBlankLayout("Parkplatz-Raum (IHK)");
        layout.Resolution = new Resolution { Width = 1920, Height = 1080 };
        layout.BackgroundColor = "#FFFFFF";

        // Header
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Header Background",
            position: new Position { X = 0, Y = 0 },
            size: new Size { Width = 1920, Height = 100 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#003366",
                ["BorderThickness"] = 0.0
            }
        ));

        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Header Accent",
            position: new Position { X = 0, Y = 92 },
            size: new Size { Width = 1920, Height = 8 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#56bd66",
                ["BorderThickness"] = 0.0
            }
        ));

        // Location Title
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Location Name",
            position: new Position { X = 60, Y = 15 },
            size: new Size { Width = 1800, Height = 70 },
            zIndex: 2,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{location.name}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 44.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{location.name}}"
        ));

        // Statistics Bar
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Stats Bar",
            position: new Position { X = 40, Y = 120 },
            size: new Size { Width = 1840, Height = 100 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#e3ebf5",
                ["BorderThickness"] = 0.0
            }
        ));

        // Stat: Belegt
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Occupied Count",
            position: new Position { X = 300, Y = 135 },
            size: new Size { Width = 250, Height = 40 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{location.occupied}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 36.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#ea515a",
                ["TextAlign"] = "Center"
            },
            dataBinding: "{{location.occupied}}"
        ));

        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Occupied Label",
            position: new Position { X = 300, Y = 175 },
            size: new Size { Width = 250, Height = 30 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Belegt",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 16.0,
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            }
        ));

        // Stat: Frei
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Free Count",
            position: new Position { X = 835, Y = 135 },
            size: new Size { Width = 250, Height = 40 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{location.free}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 36.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#56bd66",
                ["TextAlign"] = "Center"
            },
            dataBinding: "{{location.free}}"
        ));

        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Free Label",
            position: new Position { X = 835, Y = 175 },
            size: new Size { Width = 250, Height = 30 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Frei",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 16.0,
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            }
        ));

        // Stat: Gesamt
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Total Count",
            position: new Position { X = 1370, Y = 135 },
            size: new Size { Width = 250, Height = 40 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{location.total}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 36.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            },
            dataBinding: "{{location.total}}"
        ));

        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Total Label",
            position: new Position { X = 1370, Y = 175 },
            size: new Size { Width = 250, Height = 30 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Gesamt",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 16.0,
                ["Color"] = "#003366",
                ["TextAlign"] = "Center"
            }
        ));

        // Parking Slots Grid Area
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Slots Info",
            position: new Position { X = 100, Y = 260 },
            size: new Size { Width = 1720, Height = 750 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Parkplatz-Raumanzeige\n\nNutzen Sie Scriban-Templates für dynamische Parkplatz-Grids.\n\nBeispiel Datenstruktur:\nlocation.name = \"Ernst-Schneider-Platz 1\"\nlocation.slots[] = [\n  { number: \"001\", status: \"occupied\", tenant: \"Firma A\" },\n  { number: \"002\", status: \"free\" },\n  ...\n]\n\nFarben:\n✓ Frei: #56bd66 (Grün)\n✗ Belegt: #ea515a (Rot)\n⏰ Buchbar: #2196f3 (Blau)",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 22.0,
                ["Color"] = "#335c85",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle",
                ["WordWrap"] = true
            }
        ));

        // Footer
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Footer Timestamp",
            position: new Position { X = 60, Y = 1025 },
            size: new Size { Width = 1800, Height = 35 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Aktualisiert: {{current.datetime|HH:mm:ss}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 16.0,
                ["Color"] = "#335c85",
                ["TextAlign"] = "Center"
            }
        ));

        return layout;
    }

    public static DisplayLayout CreateParkingOverview1024x600Template()
    {
        var layout = CreateBlankLayout("Parkplatz-Übersicht 1024x600 (IHK)");
        layout.Resolution = new Resolution { Width = 1024, Height = 600 };
        layout.BackgroundColor = "#FFFFFF";

        // Header
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Header Background",
            position: new Position { X = 0, Y = 0 },
            size: new Size { Width = 1024, Height = 70 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#003366",
                ["BorderThickness"] = 0.0
            }
        ));

        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Header Accent",
            position: new Position { X = 0, Y = 65 },
            size: new Size { Width = 1024, Height = 5 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#56bd66",
                ["BorderThickness"] = 0.0
            }
        ));

        // Title
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Title",
            position: new Position { X = 20, Y = 10 },
            size: new Size { Width = 984, Height = 50 },
            zIndex: 2,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Parkplatz Verwaltung",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 28.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            }
        ));

        // Stats compact
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Stats Background",
            position: new Position { X = 20, Y = 85 },
            size: new Size { Width = 984, Height = 80 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#e3ebf5",
                ["BorderThickness"] = 0.0
            }
        ));

        // Compact stats layout
        var statWidth = 230.0;
        var statSpacing = 246.0;
        var statY = 95.0;

        // Total
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Total",
            position: new Position { X = 35, Y = statY },
            size: new Size { Width = statWidth, Height = 60 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{stats.total}}\nGesamt",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 20.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#003366",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{stats.total}}"
        ));

        // Occupied
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Occupied",
            position: new Position { X = 35 + statSpacing, Y = statY },
            size: new Size { Width = statWidth, Height = 60 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{stats.occupied}}\nBelegt",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 20.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#ea515a",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{stats.occupied}}"
        ));

        // Free
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Free",
            position: new Position { X = 35 + statSpacing * 2, Y = statY },
            size: new Size { Width = statWidth, Height = 60 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{stats.free}}\nFrei",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 20.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#56bd66",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{stats.free}}"
        ));

        // Usage
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Stat Usage",
            position: new Position { X = 35 + statSpacing * 3, Y = statY },
            size: new Size { Width = statWidth, Height = 60 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{stats.usage_percent}}%\nAuslastung",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 20.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#003366",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{stats.usage_percent}}"
        ));

        // Content area
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Content",
            position: new Position { X = 40, Y = 185 },
            size: new Size { Width = 944, Height = 370 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Kompakt-Übersicht 1024x600\n\nOptimiert für kleinere Displays (Raspberry Pi 7\")\n\nVerbinden Sie eine Datenquelle für dynamische Inhalte.",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 18.0,
                ["Color"] = "#335c85",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle",
                ["WordWrap"] = true
            }
        ));

        // Footer
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Footer",
            position: new Position { X = 20, Y = 565 },
            size: new Size { Width = 984, Height = 25 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{current.datetime|dd.MM.yyyy HH:mm}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 14.0,
                ["Color"] = "#335c85",
                ["TextAlign"] = "Center"
            }
        ));

        return layout;
    }

    public static DisplayLayout CreateParkingRoom1024x600Template()
    {
        var layout = CreateBlankLayout("Parkplatz-Raum 1024x600 (IHK)");
        layout.Resolution = new Resolution { Width = 1024, Height = 600 };
        layout.BackgroundColor = "#FFFFFF";

        // Header
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Header Background",
            position: new Position { X = 0, Y = 0 },
            size: new Size { Width = 1024, Height = 60 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#003366",
                ["BorderThickness"] = 0.0
            }
        ));

        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Header Accent",
            position: new Position { X = 0, Y = 55 },
            size: new Size { Width = 1024, Height = 5 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#56bd66",
                ["BorderThickness"] = 0.0
            }
        ));

        // Location Name
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Location Name",
            position: new Position { X = 20, Y = 8 },
            size: new Size { Width = 984, Height = 44 },
            zIndex: 2,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{location.name}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 26.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#FFFFFF",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{location.name}}"
        ));

        // Stats compact
        layout.Elements.Add(CreateElement(
            type: "rectangle",
            name: "Stats Bar",
            position: new Position { X = 20, Y = 70 },
            size: new Size { Width = 984, Height = 60 },
            zIndex: 0,
            properties: new Dictionary<string, object>
            {
                ["FillColor"] = "#e3ebf5",
                ["BorderThickness"] = 0.0
            }
        ));

        var statWidth = 320.0;
        var statY = 78.0;

        // Occupied
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Occupied",
            position: new Position { X = 35, Y = statY },
            size: new Size { Width = statWidth, Height = 44 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{location.occupied}} Belegt",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 22.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#ea515a",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{location.occupied}}"
        ));

        // Free
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Free",
            position: new Position { X = 352, Y = statY },
            size: new Size { Width = statWidth, Height = 44 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{location.free}} Frei",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 22.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#56bd66",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{location.free}}"
        ));

        // Total
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Total",
            position: new Position { X = 669, Y = statY },
            size: new Size { Width = statWidth, Height = 44 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{location.total}} Gesamt",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 22.0,
                ["FontWeight"] = "Bold",
                ["Color"] = "#003366",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle"
            },
            dataBinding: "{{location.total}}"
        ));

        // Parking slots area
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Slots Info",
            position: new Position { X = 40, Y = 150 },
            size: new Size { Width = 944, Height = 410 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "Raum-Anzeige 1024x600\n\nKompakt-Layout für 7\" Displays\n\nNutzen Sie Scriban-Templates\nfür dynamische Parkplatz-Grids",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 18.0,
                ["Color"] = "#335c85",
                ["TextAlign"] = "Center",
                ["VerticalAlign"] = "Middle",
                ["WordWrap"] = true
            }
        ));

        // Footer
        layout.Elements.Add(CreateElement(
            type: "text",
            name: "Footer",
            position: new Position { X = 20, Y = 570 },
            size: new Size { Width = 984, Height = 20 },
            zIndex: 1,
            properties: new Dictionary<string, object>
            {
                ["Content"] = "{{current.datetime|HH:mm:ss}}",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 12.0,
                ["Color"] = "#335c85",
                ["TextAlign"] = "Center"
            }
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
            CreateDirectoryTemplate(),
            CreateParkingOverviewTemplate(),
            CreateParkingRoomTemplate(),
            CreateParkingOverview1024x600Template(),
            CreateParkingRoom1024x600Template()
        };
    }
}
