using System;
using System.Threading.Tasks;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DigitalSignage.Tests.Services;

public class LayoutServiceTests
{
    private readonly LayoutService _layoutService;

    public LayoutServiceTests()
    {
        _layoutService = new LayoutService(NullLogger<LayoutService>.Instance);
    }

    [Fact]
    public async Task CreateLayout_ShouldGenerateUniqueId()
    {
        // Arrange
        var layout = new DisplayLayout { Name = "Test Layout" };

        // Act
        var created = await _layoutService.CreateLayoutAsync(layout);

        // Assert
        created.Id.Should().NotBeNullOrEmpty();
        created.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetLayoutById_ShouldReturnCorrectLayout()
    {
        // Arrange
        var layout = new DisplayLayout { Name = "Test Layout" };
        var created = await _layoutService.CreateLayoutAsync(layout);

        // Act
        var retrieved = await _layoutService.GetLayoutByIdAsync(created.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
        retrieved.Name.Should().Be("Test Layout");
    }

    [Fact]
    public async Task UpdateLayout_ShouldModifyExistingLayout()
    {
        // Arrange
        var layout = new DisplayLayout { Name = "Original" };
        var created = await _layoutService.CreateLayoutAsync(layout);
        created.Name = "Updated";

        // Act
        var updated = await _layoutService.UpdateLayoutAsync(created);

        // Assert
        updated.Name.Should().Be("Updated");
        updated.Modified.Should().BeAfter(updated.Created);
    }

    [Fact]
    public async Task DeleteLayout_ShouldRemoveLayout()
    {
        // Arrange
        var layout = new DisplayLayout { Name = "To Delete" };
        var created = await _layoutService.CreateLayoutAsync(layout);

        // Act
        var deleted = await _layoutService.DeleteLayoutAsync(created.Id);
        var retrieved = await _layoutService.GetLayoutByIdAsync(created.Id);

        // Assert
        deleted.IsSuccess.Should().BeTrue();
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DuplicateLayout_ShouldCreateCopyWithNewId()
    {
        // Arrange
        var original = new DisplayLayout { Name = "Original" };
        var created = await _layoutService.CreateLayoutAsync(original);

        // Act
        var duplicate = await _layoutService.DuplicateLayoutAsync(created.Id, "Copy");

        // Assert
        duplicate.Should().NotBeNull();
        duplicate.Id.Should().NotBe(created.Id);
        duplicate.Name.Should().Be("Copy");
    }

    [Fact]
    public async Task ExportLayout_ShouldReturnJsonString()
    {
        // Arrange
        var layout = new DisplayLayout { Name = "Export Test" };
        var created = await _layoutService.CreateLayoutAsync(layout);

        // Act
        var json = await _layoutService.ExportLayoutAsync(created.Id);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Export Test");
        json.Should().Contain("\"Name\"");
    }

    [Fact]
    public async Task ImportLayout_ShouldCreateFromJson()
    {
        // Arrange
        var json = @"{
            ""Name"": ""Imported Layout"",
            ""Version"": ""1.0"",
            ""Resolution"": {
                ""Width"": 1920,
                ""Height"": 1080
            },
            ""Elements"": [],
            ""DataSources"": []
        }";

        // Act
        var imported = await _layoutService.ImportLayoutAsync(json);

        // Assert
        imported.Should().NotBeNull();
        imported.Name.Should().Be("Imported Layout");
        imported.Id.Should().NotBeNullOrEmpty();
    }
}
