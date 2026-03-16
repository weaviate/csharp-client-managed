namespace Weaviate.Client.Managed.Attributes;

public enum VectorCombination
{
    None = 0,
    Sum,
    Average,
    Minimum,
    RelativeScore,
    ManualWeights,
}
