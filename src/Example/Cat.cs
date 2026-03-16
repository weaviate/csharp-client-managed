using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;

namespace Example;

[WeaviateCollection(Name = "Cat", Description = "Lots of Cats of multiple breeds")]
public record Cat
{
    [Property(DataType.Int, Description = "A counter property", Name = "counter")]
    public int Counter { get; set; }

    [Property(Description = "The color of the cat")]
    public string? Color { get; set; }

    [Property(Description = "The breed of the cat")]
    public string? Breed { get; set; }

    [Property(Name = "name", Description = "The name of the cat")]
    public string? Name { get; set; }

    [Vector<Vectorizer.SelfProvided>()]
    public float[] DefaultVector { get; set; } = [];

    // Metadata properties - automatically populated when using WithMetadata() + Execute()
    [MetadataProperty]
    public double? Score { get; set; }

    [MetadataProperty]
    public double? Distance { get; set; }

    public override string ToString()
    {
        return $"Cat ({Counter}) {{ Name: {Name}, Color: {Color}, Breed: {Breed} }}";
    }

    public string ToStringWithMetadata()
    {
        return $"Cat ({Counter}) {{ Name: {Name}, Color: {Color}, Breed: {Breed}, Score: {Score}, Distance: {Distance} }}";
    }
}
