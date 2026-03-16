using Xunit;

namespace Weaviate.Client.Managed.Tests.Context;

public class WeaviateContextOptionsTests
{
    [Fact]
    public void WeaviateContextOptions_DefaultValues()
    {
        // Act
        var options = new WeaviateContextOptions();

        // Assert
        Assert.False(options.AutoCreateCollections);
        Assert.False(options.AutoMigrate);
        Assert.False(options.AllowBreakingMigrations);
    }

    [Fact]
    public void WeaviateContextOptionsBuilder_UseAutoCreate_SetsOption()
    {
        // Arrange
        var options = new WeaviateContextOptions();
        var builder = new WeaviateContextOptionsBuilder(options);

        // Act
        builder.UseAutoCreate();

        // Assert
        Assert.True(options.AutoCreateCollections);
    }

    [Fact]
    public void WeaviateContextOptionsBuilder_UseAutoCreate_False_DisablesOption()
    {
        // Arrange
        var options = new WeaviateContextOptions { AutoCreateCollections = true };
        var builder = new WeaviateContextOptionsBuilder(options);

        // Act
        builder.UseAutoCreate(false);

        // Assert
        Assert.False(options.AutoCreateCollections);
    }

    [Fact]
    public void WeaviateContextOptionsBuilder_UseAutoMigrate_SetsOptions()
    {
        // Arrange
        var options = new WeaviateContextOptions();
        var builder = new WeaviateContextOptionsBuilder(options);

        // Act
        builder.UseAutoMigrate(true, allowBreaking: true);

        // Assert
        Assert.True(options.AutoMigrate);
        Assert.True(options.AllowBreakingMigrations);
    }

    [Fact]
    public void WeaviateContextOptionsBuilder_UseAutoMigrate_DefaultsBreakingToFalse()
    {
        // Arrange
        var options = new WeaviateContextOptions();
        var builder = new WeaviateContextOptionsBuilder(options);

        // Act
        builder.UseAutoMigrate();

        // Assert
        Assert.True(options.AutoMigrate);
        Assert.False(options.AllowBreakingMigrations);
    }

    [Fact]
    public void WeaviateContextOptionsBuilder_Chaining_Works()
    {
        // Arrange
        var options = new WeaviateContextOptions();
        var builder = new WeaviateContextOptionsBuilder(options);

        // Act
        var result = builder.UseAutoCreate().UseAutoMigrate(true, allowBreaking: false);

        // Assert
        Assert.Same(builder, result);
        Assert.True(options.AutoCreateCollections);
        Assert.True(options.AutoMigrate);
        Assert.False(options.AllowBreakingMigrations);
    }
}
