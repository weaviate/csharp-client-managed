using Xunit;

namespace Weaviate.Client.Managed.Tests.Context;

public class IdPropertyHelperTests
{
    #region GetId Tests

    [Fact]
    public void GetId_WithUUIDProperty_ReturnsValue()
    {
        // Arrange
        var entity = new EntityWithUUID { UUID = Guid.NewGuid() };

        // Act
        var id = IdPropertyHelper.GetId(entity);

        // Assert
        Assert.Equal(entity.UUID, id);
    }

    [Fact]
    public void GetId_WithWeaviateUUIDAttribute_ReturnsValue()
    {
        // Arrange
        var entity = new EntityWithWeaviateUUIDAttribute { MyId = Guid.NewGuid() };

        // Act
        var id = IdPropertyHelper.GetId(entity);

        // Assert
        Assert.Equal(entity.MyId, id);
    }

    [Fact]
    public void GetId_WithIdPropertyOnAttribute_ReturnsValue()
    {
        // Arrange
        var entity = new EntityWithIdPropertyAttribute { BookId = Guid.NewGuid() };

        // Act
        var id = IdPropertyHelper.GetId(entity);

        // Assert
        Assert.Equal(entity.BookId, id);
    }

    [Fact]
    public void GetId_WithIdPropertyFallback_ReturnsValue()
    {
        // Arrange
        var entity = new EntityWithIdFallback { Id = Guid.NewGuid() };

        // Act
        var id = IdPropertyHelper.GetId(entity);

        // Assert
        Assert.Equal(entity.Id, id);
    }

    [Fact]
    public void GetId_WithEmptyGuid_ReturnsEmptyGuid()
    {
        // Arrange
        var entity = new EntityWithUUID { UUID = Guid.Empty };

        // Act
        var id = IdPropertyHelper.GetId(entity);

        // Assert
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void GetId_WithNoIdProperty_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = new EntityWithNoId { Name = "Test" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => IdPropertyHelper.GetId(entity));
        Assert.Contains("does not have a valid UUID property", ex.Message);
    }

    #endregion

    #region SetId Tests

    [Fact]
    public void SetId_WithUUIDProperty_SetsValue()
    {
        // Arrange
        var entity = new EntityWithUUID();
        var newId = Guid.NewGuid();

        // Act
        IdPropertyHelper.SetId(entity, newId);

        // Assert
        Assert.Equal(newId, entity.UUID);
    }

    [Fact]
    public void SetId_WithWeaviateUUIDAttribute_SetsValue()
    {
        // Arrange
        var entity = new EntityWithWeaviateUUIDAttribute();
        var newId = Guid.NewGuid();

        // Act
        IdPropertyHelper.SetId(entity, newId);

        // Assert
        Assert.Equal(newId, entity.MyId);
    }

    [Fact]
    public void SetId_WithNullableGuid_SetsValue()
    {
        // Arrange
        var entity = new EntityWithNullableUUID();
        var newId = Guid.NewGuid();

        // Act
        IdPropertyHelper.SetId(entity, newId);

        // Assert
        Assert.Equal(newId, entity.UUID);
    }

    #endregion

    #region HasValidId Tests

    [Fact]
    public void HasValidId_WithValidGuid_ReturnsTrue()
    {
        // Arrange
        var entity = new EntityWithUUID { UUID = Guid.NewGuid() };

        // Act
        var result = IdPropertyHelper.HasValidId(entity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasValidId_WithEmptyGuid_ReturnsFalse()
    {
        // Arrange
        var entity = new EntityWithUUID { UUID = Guid.Empty };

        // Act
        var result = IdPropertyHelper.HasValidId(entity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasValidId_WithNoIdProperty_ReturnsFalse()
    {
        // Arrange
        var entity = new EntityWithNoId { Name = "Test" };

        // Act
        var result = IdPropertyHelper.HasValidId(entity);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetIdProperty Tests

    [Fact]
    public void GetIdProperty_WeaviateUUIDAttribute_TakesPrecedence()
    {
        // Act
        var property = IdPropertyHelper.GetIdProperty(typeof(EntityWithMultipleIdCandidates));

        // Assert
        Assert.NotNull(property);
        Assert.Equal("CustomId", property.Name);
    }

    [Fact]
    public void GetIdProperty_IdPropertyAttribute_TakesPrecedenceOverConvention()
    {
        // Act
        var property = IdPropertyHelper.GetIdProperty(typeof(EntityWithIdPropertyAttribute));

        // Assert
        Assert.NotNull(property);
        Assert.Equal("BookId", property.Name);
    }

    [Fact]
    public void GetIdProperty_CaseInsensitive()
    {
        // Act
        var property = IdPropertyHelper.GetIdProperty(typeof(EntityWithLowercaseUuid));

        // Assert
        Assert.NotNull(property);
        Assert.Equal("uuid", property.Name);
    }

    #endregion

    #region Test Entities

    [WeaviateCollection]
    private class EntityWithUUID
    {
        public Guid UUID { get; set; }
        public string? Name { get; set; }
    }

    [WeaviateCollection]
    private class EntityWithNullableUUID
    {
        public Guid? UUID { get; set; }
        public string? Name { get; set; }
    }

    [WeaviateCollection]
    private class EntityWithWeaviateUUIDAttribute
    {
        [WeaviateUUID]
        public Guid MyId { get; set; }
        public string? Name { get; set; }
    }

    [WeaviateCollection(IdProperty = nameof(BookId))]
    private class EntityWithIdPropertyAttribute
    {
        public Guid BookId { get; set; }
        public string? Title { get; set; }
    }

    [WeaviateCollection]
    private class EntityWithIdFallback
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
    }

    [WeaviateCollection]
    private class EntityWithNoId
    {
        public string? Name { get; set; }
    }

    [WeaviateCollection]
    private class EntityWithMultipleIdCandidates
    {
        public Guid UUID { get; set; }
        public Guid Id { get; set; }

        [WeaviateUUID]
        public Guid CustomId { get; set; }
    }

    [WeaviateCollection]
    private class EntityWithLowercaseUuid
    {
        public Guid uuid { get; set; }
    }

    #endregion
}
