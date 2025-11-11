using DigitalSignage.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace DigitalSignage.Tests.Helpers;

public class LayoutTemplatesTests
{
    [Fact]
    public void CreateBlankLayout_ShouldReturnValidLayout()
    {
        // Act
        var layout = LayoutTemplates.CreateBlankLayout();

        // Assert
        layout.Should().NotBeNull();
        layout.Name.Should().Be("Blank Layout");
        layout.Resolution.Width.Should().Be(1920);
        layout.Resolution.Height.Should().Be(1080);
        layout.Elements.Should().BeEmpty();
    }

    [Fact]
    public void CreateRoomDisplayTemplate_ShouldContainRequiredElements()
    {
        // Act
        var layout = LayoutTemplates.CreateRoomDisplayTemplate();

        // Assert
        layout.Should().NotBeNull();
        layout.Name.Should().Be("Room Display");
        layout.Elements.Should().NotBeEmpty();
        layout.Elements.Should().Contain(e => e.Name == "Room Name");
        layout.Elements.Should().Contain(e => e.Name == "Status");
    }

    [Fact]
    public void CreateWelcomeScreenTemplate_ShouldIncludeQRCode()
    {
        // Act
        var layout = LayoutTemplates.CreateWelcomeScreenTemplate();

        // Assert
        layout.Elements.Should().Contain(e => e.Type == "qrcode");
        layout.Elements.Should().Contain(e => e.Name == "QR Code");
    }

    [Fact]
    public void CreateInformationBoardTemplate_ShouldHaveHeaderAndContent()
    {
        // Act
        var layout = LayoutTemplates.CreateInformationBoardTemplate();

        // Assert
        layout.Elements.Should().Contain(e => e.Name == "Header Text");
        layout.Elements.Should().Contain(e => e.Name == "Content");
        layout.Elements.Should().Contain(e => e.Name == "DateTime");
    }

    [Fact]
    public void CreateDirectoryTemplate_ShouldIncludeTable()
    {
        // Act
        var layout = LayoutTemplates.CreateDirectoryTemplate();

        // Assert
        layout.Elements.Should().Contain(e => e.Type == "table");
        layout.Elements.Should().Contain(e => e.Name == "Directory Table");
    }

    [Fact]
    public void GetAllTemplates_ShouldReturnMultipleTemplates()
    {
        // Act
        var templates = LayoutTemplates.GetAllTemplates();

        // Assert
        templates.Should().NotBeNull();
        templates.Should().HaveCountGreaterThan(3);
        templates.Should().Contain(t => t.Name == "Blank Layout");
        templates.Should().Contain(t => t.Name == "Room Display");
    }

    [Fact]
    public void AllTemplates_ShouldHaveValidResolutions()
    {
        // Act
        var templates = LayoutTemplates.GetAllTemplates();

        // Assert
        foreach (var template in templates)
        {
            template.Resolution.Should().NotBeNull();
            template.Resolution.Width.Should().BeGreaterThan(0);
            template.Resolution.Height.Should().BeGreaterThan(0);
        }
    }
}
