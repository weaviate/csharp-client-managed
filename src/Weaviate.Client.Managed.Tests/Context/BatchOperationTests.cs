using Xunit;

namespace Weaviate.Client.Managed.Tests.Context;

public class BatchOperationTests
{
    [Fact]
    public void InsertOperation_StoresEntitiesAndType()
    {
        // Arrange
        var entities = new[]
        {
            new TestEntity { Name = "Entity1" },
            new TestEntity { Name = "Entity2" },
        };

        // Act
        var operation = new InsertOperation<TestEntity>(entities);

        // Assert
        Assert.Equal(typeof(TestEntity), operation.EntityType);
        Assert.Equal(2, operation.Entities.Length);
        Assert.Equal("Entity1", operation.Entities[0].Name);
    }

    [Fact]
    public void UpdateOperation_StoresEntitiesAndType()
    {
        // Arrange
        var entities = new[] { new TestEntity { Name = "Updated" } };

        // Act
        var operation = new UpdateOperation<TestEntity>(entities);

        // Assert
        Assert.Equal(typeof(TestEntity), operation.EntityType);
        Assert.Single(operation.Entities);
    }

    [Fact]
    public void DeleteOperation_StoresIdsAndType()
    {
        // Arrange
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var operation = new DeleteOperation<TestEntity>(ids);

        // Assert
        Assert.Equal(typeof(TestEntity), operation.EntityType);
        Assert.Equal(2, operation.Ids.Length);
    }

    [Fact]
    public void BatchOperations_AreRecords_WithValueEquality()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test" };
        var entities = new[] { entity };

        // Act
        var op1 = new InsertOperation<TestEntity>(entities);
        var op2 = new InsertOperation<TestEntity>(entities);

        // Assert - records use value equality
        Assert.Equal(op1, op2);
    }

    [WeaviateCollection]
    private class TestEntity
    {
        public Guid UUID { get; set; }
        public string? Name { get; set; }
    }
}
