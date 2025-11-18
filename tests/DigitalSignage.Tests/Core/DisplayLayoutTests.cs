using DigitalSignage.Core.Models;
using FluentAssertions;
using Xunit;

namespace DigitalSignage.Tests.Core;

public class DisplayLayoutTests
{
    [Fact]
    public void DisplayLayout_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var layout = new DisplayLayout();

        // Assert
        layout.Should().NotBeNull();
        layout.Id.Should().NotBeNullOrEmpty();
        layout.Version.Should().Be("1.0");
        layout.Elements.Should().NotBeNull().And.BeEmpty();
        layout.DataSources.Should().NotBeNull().And.BeEmpty();
        layout.Resolution.Should().NotBeNull();
        layout.Resolution.Width.Should().Be(1920);
        layout.Resolution.Height.Should().Be(1080);
    }

    [Fact]
    public void DisplayLayout_ShouldAllowAddingElements()
    {
        // Arrange
        var layout = new DisplayLayout();
        var element = new DisplayElement
        {
            Type = "text",
            Name = "Test Element"
        };

        // Act
        layout.Elements.Add(element);

        // Assert
        layout.Elements.Should().HaveCount(1);
        layout.Elements[0].Should().Be(element);
    }

    [Fact]
    public void DisplayElement_ShouldHaveDefaultProperties()
    {
        // Arrange & Act
        var element = new DisplayElement();

        // Assert
        element.Id.Should().NotBeNullOrEmpty();
        element.ZIndex.Should().Be(0);
        element.Opacity.Should().Be(1.0);
        element.Rotation.Should().Be(0);
        element.Visible.Should().BeTrue();
        element.Position.Should().NotBeNull();
        element.Size.Should().NotBeNull();
    }

    [Fact]
    public void Resolution_ShouldSupportLandscapeAndPortrait()
    {
        // Arrange & Act
        var landscape = new Resolution
        {
            Width = 1920,
            Height = 1080,
            Orientation = "landscape"
        };

        var portrait = new Resolution
        {
            Width = 1080,
            Height = 1920,
            Orientation = "portrait"
        };

        // Assert
        landscape.Orientation.Should().Be("landscape");
        portrait.Orientation.Should().Be("portrait");
    }

    [Fact]
    public void DisplayLayout_ShouldSupportMultipleDataSources()
    {
        // Arrange
        var layout = new DisplayLayout();
        var dataSource1 = new DataSource
        {
            Name = "SQL Source",
            Type = DataSourceType.SQL
        };
        var dataSource2 = new DataSource
        {
            Name = "REST Source",
            Type = DataSourceType.REST
        };

        // Act
        layout.DataSources.Add(dataSource1);
        layout.DataSources.Add(dataSource2);

        // Assert
        layout.DataSources.Should().HaveCount(2);
        layout.DataSources.Should().Contain(ds => ds.Type == DataSourceType.SQL);
        layout.DataSources.Should().Contain(ds => ds.Type == DataSourceType.REST);
    }
}
