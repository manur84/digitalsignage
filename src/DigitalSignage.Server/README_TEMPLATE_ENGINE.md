# Template Engine Integration

## Required NuGet Package

The Template Engine functionality requires the **Scriban** NuGet package:

```bash
dotnet add package Scriban
```

Or via Package Manager Console:

```powershell
Install-Package Scriban
```

## Package Details

- **Package**: Scriban
- **Version**: Latest stable (>=5.0.0 recommended)
- **License**: BSD-2-Clause
- **Repository**: https://github.com/scriban/scriban

## Installation

### Via .NET CLI

```bash
cd src/DigitalSignage.Server
dotnet add package Scriban
dotnet restore
```

### Via Visual Studio

1. Right-click on `DigitalSignage.Server` project
2. Select "Manage NuGet Packages"
3. Search for "Scriban"
4. Click "Install"

### Via Package Reference (.csproj)

Add this to your `DigitalSignage.Server.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Scriban" Version="5.9.1" />
</ItemGroup>
```

Then run:
```bash
dotnet restore
```

## Usage

The `TemplateService` is automatically registered in the DI container (see `App.xaml.cs`).

### Example Usage

```csharp
// Inject ITemplateService
private readonly ITemplateService _templateService;

// Process a template
var template = "Hello {{Name}}, you have {{Count}} messages";
var data = new Dictionary<string, object>
{
    ["Name"] = "Max",
    ["Count"] = 5
};

var result = await _templateService.ProcessTemplateAsync(template, data);
// Result: "Hello Max, you have 5 messages"
```

## Features

- Variable replacement: `{{VariableName}}`
- Conditional logic: `{{if condition}}...{{end}}`
- Loops: `{{for item in items}}...{{end}}`
- Functions: `{{date_format Date "dd.MM.yyyy"}}`
- Math operations: `{{Value1 + Value2}}`
- Default values: `{{Variable ?? "default"}}`

## Documentation

See [TEMPLATE_ENGINE.md](../../docs/TEMPLATE_ENGINE.md) for complete documentation with examples.

## Troubleshooting

### Build Error: "The type or namespace name 'Scriban' could not be found"

**Solution:** Install the Scriban NuGet package:
```bash
dotnet add package Scriban
dotnet restore
```

### Runtime Error: "Could not load file or assembly 'Scriban'"

**Solution:** Ensure the package is restored:
```bash
dotnet restore
dotnet build
```

### Template Parsing Errors

Check server logs for detailed error messages:
```
logs/digitalsignage-*.txt
```

## Performance

- Templates are compiled and cached automatically
- First render may be slower, subsequent renders are fast
- Consider increasing `RefreshInterval` for complex templates

## Security

- Templates are executed in a sandboxed environment
- No access to file system or network
- No reflection or dynamic code execution
- Safe for user-provided templates

## More Information

- [Scriban GitHub](https://github.com/scriban/scriban)
- [Scriban Documentation](https://github.com/scriban/scriban/tree/master/doc)
- [Language Reference](https://github.com/scriban/scriban/blob/master/doc/language.md)
