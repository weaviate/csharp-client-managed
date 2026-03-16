# Attributes Reference

Complete reference for all managed attributes.

## Overview

| Attribute | Target | Purpose |
| --- | --- | --- |
| [`WeaviateCollection`](#weaviatecollection) | Class | Collection name, sharding, replication, multi-tenancy |
| [`InvertedIndex`](#invertedindex) | Class | Inverted index settings (timestamps, null state) |
| [`Generative<T>`](#generativet) | Class | RAG configuration (15+ providers) |
| [`Reranker<T>`](#rerankert) | Class | Reranking configuration (6 providers) |
| [`QueryProjection<T>`](#queryprojectt) | Class | Query projection type for a collection entity |
| [`Property`](#property) | Property | Data type, name, description |
| [`Index`](#index) | Property | Filtering, searching, range filters |
| [`Tokenization`](#tokenization) | Property | Text tokenization strategy |
| [`Vector<T>`](#vectort) | Property | Named vector with vectorizer |
| [`VectorIndex<T>`](#vectorindext) | Property | Vector index configuration (HNSW, Flat, Dynamic) |
| [`Quantizer*`](#quantizers) | Property | Vector compression (BQ, PQ, SQ, RQ) |
| [`Encoding`](#encoding) | Property | Multi-vector (ColBERT) configuration |
| [`Reference`](#reference) | Property | Cross-reference to another collection |
| [`NestedType`](#nestedtype) | Property | Nested object type override |
| [`MetadataProperty`](#metadataproperty) | Property | Inject query metadata (score, distance) |
| [`WeaviateUUID`](#weaviateuuid) | Property | Marks a property as the entity's UUID |
| [`MapFrom`](#mapfrom) | Property | Maps projection property from a different source name |
| [`Vector` (projection)](#vector-projection) | Property | Includes a named vector in projection results |
| [`Reference` (projection)](#reference-projection) | Property | Includes a reference in projection results |
| [`QueryAggregate<T>`](#queryaggregate) | Class | Marks a type as an aggregation projection for a collection |
| [`Metrics`](#metricsattribute) | Property | Specifies which aggregate metrics to compute (Number, Integer, Text, Boolean, Date) |

---

## Class-Level Attributes

### WeaviateCollection

Defines a Weaviate collection. Required on model classes.

```csharp
[WeaviateCollection("Articles", Description = "Blog articles")]
public class Article { }
```

#### Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Name` | `string?` | Class name | Collection name in Weaviate |
| `Description` | `string?` | null | Collection description |
| **Multi-Tenancy** | | | |
| `MultiTenancyEnabled` | `bool?` | false | Enable multi-tenancy (immutable) |
| `AutoTenantCreation` | `bool?` | null | Auto-create tenants on reference |
| `AutoTenantActivation` | `bool?` | null | Auto-activate created tenants |
| **Sharding** | | | |
| `ShardingDesiredCount` | `int` | -1 (default) | Number of shards (immutable) |
| `ShardingVirtualPerPhysical` | `int` | -1 (default) | Virtual shards per physical |
| `ShardingDesiredVirtualCount` | `int` | -1 (default) | Total virtual shards |
| `ShardingKey` | `string?` | "_id" | Property to shard on |
| **Replication** | | | |
| `ReplicationFactor` | `int` | -1 (default) | Number of replicas (immutable) |
| `ReplicationAsyncEnabled` | `bool` | false | Enable async replication |
| **Lifecycle** | | | |
| `CollectionConfigMethod` | `string?` | null | Static method name for lifecycle hooks |
| `ConfigMethodClass` | `Type?` | null | Class containing the config method |

#### CollectionConfigMethod Signature

The `CollectionConfigMethod` must point to a static method that accepts an `OnCollectionConfig` parameter:

```csharp
[WeaviateCollection("Products", CollectionConfigMethod = nameof(OnConfig))]
public class Product
{
    [Property]
    public string Name { get; set; } = "";

    public static void OnConfig(OnCollectionConfig config)
    {
        config.OnCreate(createParams =>
        {
            // Modify collection creation params before they're sent to Weaviate
            return createParams;
        });
    }
}
```

#### Examples

```csharp
// Basic
[WeaviateCollection("Products")]
public class Product { }

// Multi-tenant
[WeaviateCollection("Orders",
    MultiTenancyEnabled = true,
    AutoTenantCreation = true,
    AutoTenantActivation = true)]
public class Order { }

// Sharded and replicated
[WeaviateCollection("Events",
    ShardingDesiredCount = 3,
    ReplicationFactor = 2)]
public class Event { }
```

---

### InvertedIndex

Configures inverted index settings at collection level.

```csharp
[WeaviateCollection("Articles")]
[InvertedIndex(IndexTimestamps = true)]
public class Article { }
```

#### Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `IndexTimestamps` | `bool` | false | Index creation/update timestamps |
| `IndexNullState` | `bool` | false | Index null values for filtering |
| `IndexPropertyLength` | `bool` | false | Index property lengths |
| `CleanupIntervalSeconds` | `int` | -1 (default) | Cleanup interval |

---

### Generative\<T\>

Configures RAG (Retrieval Augmented Generation) module.

```csharp
[WeaviateCollection("Articles")]
[Generative<GenerativeConfig.OpenAI>(Model = "gpt-4")]
public class Article { }
```

#### Supported Providers

| Type | Provider |
| --- | --- |
| `GenerativeConfig.OpenAI` | OpenAI |
| `GenerativeConfig.AzureOpenAI` | Azure OpenAI |
| `GenerativeConfig.Anthropic` | Anthropic Claude |
| `GenerativeConfig.Cohere` | Cohere |
| `GenerativeConfig.AWS` | AWS Bedrock |
| `GenerativeConfig.Google` | Google AI |
| `GenerativeConfig.Mistral` | Mistral AI |
| `GenerativeConfig.Ollama` | Ollama (local) |
| `GenerativeConfig.Nvidia` | NVIDIA |
| `GenerativeConfig.Databricks` | Databricks |
| `GenerativeConfig.Anyscale` | Anyscale |
| `GenerativeConfig.FriendlyAI` | FriendliAI |
| `GenerativeConfig.OctoAI` | OctoAI |
| `GenerativeConfig.XAI` | xAI |

#### Common Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Model` | `string?` | null | Model name |
| `BaseURL` | `string?` | null | Custom API endpoint |
| `MaxTokens` | `int` | -1 (default) | Max tokens to generate |
| `Temperature` | `double` | -1 (default) | Randomness (0-1) |
| `TopP` | `double` | -1 (default) | Nucleus sampling |
| `TopK` | `int` | -1 (default) | Token sampling |

#### Provider-Specific Properties

| Property | Providers | Description |
| --- | --- | --- |
| `ResourceName` | AzureOpenAI | Azure resource name |
| `DeploymentId` | AzureOpenAI | Azure deployment ID |
| `ProjectId` | Google | Google Cloud project |
| `Region` | AWS | AWS region |
| `Service` | AWS | AWS service (bedrock) |

---

### Reranker\<T\>

Configures reranking module for result re-ordering.

```csharp
[WeaviateCollection("Articles")]
[Reranker<RerankerConfig.Cohere>(Model = "rerank-english-v3.0")]
public class Article { }
```

#### Supported Providers

| Type | Provider |
| --- | --- |
| `RerankerConfig.Cohere` | Cohere |
| `RerankerConfig.VoyageAI` | Voyage AI |
| `RerankerConfig.JinaAI` | Jina AI |
| `RerankerConfig.Nvidia` | NVIDIA |
| `RerankerConfig.ContextualAI` | Contextual AI |
| `RerankerConfig.Transformers` | Local transformers |

#### Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Model` | `string?` | null | Model name |
| `BaseURL` | `string?` | null | Custom API endpoint |
| `TopN` | `int` | -1 (default) | Return top N results |
| `Instruction` | `string?` | null | Query instruction (some providers) |

---

### QueryProject\<T\>

Marks a type as a query projection for a specific collection entity type. Projections allow selecting a subset of properties with optional renaming, metadata injection, and vector inclusion.

```csharp
[QueryProjection<Article>]
public class ArticleSummary
{
    public string Title { get; set; } = "";

    [MapFrom(nameof(Article.WordCount))]
    public int Words { get; set; }

    [MetadataProperty]
    public double? Score { get; set; }
}
```

Use projections with `Project<TProjection>()`:

```csharp
var summaries = await context.Products.Project<ArticleSummary>()
    .NearText("search term")
    .Execute();
```

---

## Property-Level Attributes

### Property

Defines a Weaviate property. Required for all stored properties.

```csharp
[Property(DataType.Text)]
public string Title { get; set; }

// Type inference (recommended)
[Property]
public string Title { get; set; }
```

#### Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `DataType` | `DataType` | `Unknown` (inferred) | Weaviate data type |
| `Name` | `string?` | Property name (camelCase) | Custom property name |
| `Description` | `string?` | null | Property description |

#### Type Inference

| C# Type | Inferred DataType |
| --- | --- |
| `string` | `Text` |
| `int`, `long` | `Int` |
| `float`, `double`, `decimal` | `Number` |
| `bool` | `Bool` |
| `DateTime`, `DateTimeOffset` | `Date` |
| `Guid` | `Uuid` |
| `byte[]` | `Blob` |
| `List<string>`, `string[]` | `TextArray` |
| `List<int>`, `int[]` | `IntArray` |
| Class type | `Object` |
| `List<Class>` | `ObjectArray` |

---

### Index

Configures indexing for filtering and searching.

```csharp
[Property]
[Index(Filterable = true, Searchable = true)]
public string Title { get; set; }
```

#### Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Filterable` | `bool` | false | Enable equality/contains filters |
| `Searchable` | `bool` | false | Enable BM25 text search |
| `RangeFilters` | `bool` | false | Enable >, <, >=, <= filters |

---

### Tokenization

Specifies text tokenization strategy.

```csharp
[Property]
[Tokenization(PropertyTokenization.Word)]
public string Title { get; set; }
```

#### Values

| Value | Description |
| --- | --- |
| `Word` | Standard word tokenization (default) |
| `Lowercase` | Lowercase before tokenization |
| `Whitespace` | Split on whitespace only |
| `Field` | Treat entire field as single token |
| `Trigram` | Character trigrams |
| `Gse` | Chinese segmentation |
| `Kagome` | Japanese tokenization |

---

### Vector\<T\>

Defines a named vector with vectorizer configuration.

```csharp
[Vector<Vectorizer.Text2VecOpenAI>(
    Model = "text-embedding-ada-002",
    SourceProperties = [nameof(Title), nameof(Content)]
)]
public float[]? Embedding { get; set; }
```

#### Property Type Determines Vector Type

| C# Type | Vector Type |
| --- | --- |
| `float[]` | Single vector |
| `float[,]` | Multi-vector (ColBERT) |

#### Common Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Name` | `string?` | Property name (camelCase) | Custom vector name |
| `SourceProperties` | `string[]?` | null | Properties to vectorize |
| `VectorizeCollectionName` | `bool` | false | Include collection name |
| `Model` | `string?` | null | Model name |
| `Dimensions` | `int?` | null | Embedding dimensions |
| `BaseURL` | `string?` | null | Custom API endpoint |

#### Multi-Modal Properties

| Property | Type | Description |
| --- | --- | --- |
| `TextFields` | `string[]?` | Text fields for multi-modal |
| `ImageFields` | `string[]?` | Image fields for multi-modal |
| `VideoFields` | `string[]?` | Video fields for multi-modal |

#### Ref2Vec Properties

| Property | Type | Description |
| --- | --- | --- |
| `ReferenceProperties` | `string[]?` | Reference properties for Ref2Vec |

#### ConfigMethod

```csharp
[Vector<Vectorizer.Text2VecOpenAI>(
    Model = "text-embedding-3-small",
    ConfigMethod = nameof(ConfigureVector)
)]
public float[]? Embedding { get; set; }

public static Vectorizer.Text2VecOpenAI ConfigureVector(
    string vectorName,
    Vectorizer.Text2VecOpenAI prebuilt)
{
    prebuilt.VectorizeCollectionName = false;
    return prebuilt;
}
```

---

### VectorIndex\<T\>

Configures the vector index type.

```csharp
[Vector<Vectorizer.Text2VecOpenAI>]
[VectorIndex<VectorIndexConfig.Hnsw>(
    EfConstruction = 128,
    MaxConnections = 32
)]
public float[]? Embedding { get; set; }
```

#### Index Types

| Type | Description |
| --- | --- |
| `VectorIndexConfig.Hnsw` | HNSW index (default, recommended) |
| `VectorIndexConfig.Flat` | Brute-force exact search |
| `VectorIndexConfig.Dynamic` | Auto-switches from Flat to HNSW |

#### HNSW Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `EfConstruction` | `int` | 128 | Build-time quality |
| `MaxConnections` | `int` | 32 | Graph connectivity |
| `Ef` | `int` | -1 | Query-time quality |
| `DynamicEfMin` | `int` | 100 | Min dynamic EF |
| `DynamicEfMax` | `int` | 500 | Max dynamic EF |
| `DynamicEfFactor` | `int` | 8 | Dynamic EF multiplier |

#### Distance Metrics

| Value | Description |
| --- | --- |
| `Cosine` | Cosine similarity (default) |
| `Dot` | Dot product |
| `L2Squared` | Euclidean distance squared |
| `Hamming` | Hamming distance (binary) |

---

### Quantizers

Vector compression for reduced memory usage.

#### QuantizerBQ (Binary Quantization)

```csharp
[Vector<Vectorizer.Text2VecOpenAI>]
[QuantizerBQ(Cache = true, RescoreLimit = 200)]
public float[]? Embedding { get; set; }
```

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Cache` | `bool` | true | Cache quantized vectors |
| `RescoreLimit` | `int` | -1 | Candidates for rescoring |

#### QuantizerPQ (Product Quantization)

```csharp
[Vector<Vectorizer.Text2VecOpenAI>]
[QuantizerPQ(Segments = 128, Centroids = 256)]
public float[]? Embedding { get; set; }
```

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Segments` | `int` | 0 | Number of segments |
| `Centroids` | `int` | 256 | Centroids per segment |
| `TrainingLimit` | `int` | 100000 | Training samples |
| `EncoderType` | `PQEncoderType` | Tile | Encoder type |
| `EncoderDistribution` | `PQEncoderDistribution` | LogNormal | Distribution |

#### QuantizerSQ (Scalar Quantization)

```csharp
[Vector<Vectorizer.Text2VecOpenAI>]
[QuantizerSQ(TrainingLimit = 50000)]
public float[]? Embedding { get; set; }
```

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `TrainingLimit` | `int` | 100000 | Training samples |
| `RescoreLimit` | `int` | -1 | Candidates for rescoring |

#### QuantizerRQ (Residual Quantization)

```csharp
[Vector<Vectorizer.Text2VecOpenAI>]
[QuantizerRQ(Bits = 8)]
public float[]? Embedding { get; set; }
```

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Bits` | `int` | 8 | Bits per dimension |
| `Cache` | `bool` | true | Cache quantized vectors |
| `RescoreLimit` | `int` | -1 | Candidates for rescoring |

---

### Encoding

Configures multi-vector (ColBERT) encoding.

```csharp
[Vector<Vectorizer.SelfProvided>]
[Encoding(KSim = 3, DProjections = 512)]
public float[,]? ColBERTEmbedding { get; set; }
```

#### Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `KSim` | `int` | -1 | K for k-nearest token similarity |
| `DProjections` | `int` | -1 | Projection dimensions |
| `Repetitions` | `int` | -1 | Repetitions |

---

### Reference

Defines a cross-reference to another collection.

```csharp
// Single reference — target collection inferred from property type (Category)
[Reference]
public Category? Category { get; set; }

// Optional name override — Weaviate property name becomes "primaryCategory" by convention,
// use this only when you need a different name on the wire
[Reference("primaryCat")]
public Category? PrimaryCategory { get; set; }

// ID-only reference — Guid? cannot carry type info, so Target= is required
[Reference(Target = typeof(Author))]
public Guid? AuthorId { get; set; }

// Multi-reference — target inferred from List<Article>
[Reference]
public List<Article>? RelatedArticles { get; set; }
```

#### Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| constructor `name` | `string?` | null | Weaviate property name override (defaults to camelCase of C# property name) |
| `Target` | `Type?` | null | Explicit target type — **required** when property is `Guid?` (target cannot be inferred) |
| `Description` | `string?` | null | Reference description |
| `Loading` | `ReferenceLoadingStrategy` | `Explicit` | Loading strategy (see below) |
| `SourceProperty` | `string?` | null | Used in projections to map from a differently-named source property |

The target collection name is inferred at schema time:

1. If `Target` is set → use that type's `[WeaviateCollection]` name
2. If property type is `Guid?` and `Target` is not set → **throws** (target is ambiguous)
3. If property type is `Category?` → extract `Category`, look up its `[WeaviateCollection]` name
4. If property type is `List<Article>` → extract `Article`, look up its `[WeaviateCollection]` name

#### ReferenceLoadingStrategy

Controls when references are loaded in queries:

| Value | Description |
| --- | --- |
| `Explicit` | Default. References are only loaded when `.WithReferences()` is called on the query. |
| `Eager` | References are automatically included in every query without needing `.WithReferences()`. |

```csharp
// Explicit loading (default) — requires .WithReferences()
[Reference]
public Category? Category { get; set; }

// Eager loading — auto-included in every query
[Reference(Loading = ReferenceLoadingStrategy.Eager)]
public Category? Category { get; set; }
```

---

### NestedType

Overrides the inferred nested object type. Usually not needed.

```csharp
// Auto-inferred (recommended)
[Property(DataType.Object)]
public Author Author { get; set; }

// Explicit (for interfaces/base classes)
[Property(DataType.Object)]
[NestedType(typeof(ConcreteAuthor))]
public IAuthor Author { get; set; }
```

#### Properties

| Property | Type | Description |
| --- | --- | --- |
| `Type` | `Type` | Concrete nested type |

---

### MetadataProperty

Injects query metadata into the property.

```csharp
public class Article
{
    [Property]
    public string Title { get; set; }

    [MetadataProperty(MetadataType.Score)]
    public float? SearchScore { get; set; }

    [MetadataProperty(MetadataType.Distance)]
    public float? Distance { get; set; }
}
```

After executing a query with `.WithMetadata()`, these properties are automatically populated.

#### Values

| Value | Type | Description |
| --- | --- | --- |
| `Score` | `float?` | BM25/hybrid search score |
| `Distance` | `float?` | Vector distance |
| `Certainty` | `float?` | Vector certainty (1 - distance) |
| `CreationTime` | `DateTime?` | Object creation timestamp |
| `UpdateTime` | `DateTime?` | Last update timestamp |
| `ExplainScore` | `string?` | Score explanation |

---

### WeaviateUUID

Marks a property as the entity's Weaviate UUID. The property must be of type `Guid` or `Guid?`.

```csharp
[WeaviateCollection("Books")]
public class Book
{
    [WeaviateUUID]
    public Guid BookId { get; set; }

    [Property]
    public string Title { get; set; } = "";
}
```

By default, the context looks for a property named `UUID` (matching Weaviate convention). Use `[WeaviateUUID]` to designate a differently-named property as the ID.

When inserting via `WeaviateContext`, the ID is automatically assigned back to the entity:

```csharp
var book = new Book { Title = "My Book" };
await context.Insert(book);
Console.WriteLine(book.BookId); // Assigned Guid
```

---

### MapFrom

Specifies the source property name for a projection property when it differs from the projection property name. Used on properties within `[QueryProjection<T>]` types.

```csharp
[QueryProjection<Article>]
public class ArticleProjection
{
    [MapFrom(nameof(Article.WordCount))]
    public int Words { get; set; }
}
```

The constructor argument is the source property name in PascalCase. It will be converted to camelCase for Weaviate property lookup.

---

### Vector (projection) {#vector-projection}

Marks a property in a query projection to receive a named vector from query results. The property must be of type `float[]`.

```csharp
[QueryProjection<Article>]
public class ArticleWithEmbedding
{
    public string Title { get; set; } = "";

    [Vector(VectorName = "embedding")]
    public float[]? Embedding { get; set; }
}
```

#### Vector (projection) Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `VectorName` | `string?` | null (uses property name as camelCase) | The name of the vector to include |

---

### Reference (projection) {#reference-projection}

Marks a property in a query projection to receive a cross-reference from query results. The property can be a single object, a `List<T>`, or a `Guid?` (ID-only).

```csharp
[QueryProjection<Article>]
public class ArticleWithCategory
{
    public string Title { get; set; } = "";

    [Reference]
    public Category? Category { get; set; }

    // Rename source property
    [Reference(SourceProperty = "RelatedArticles")]
    public List<Article>? Related { get; set; }
}
```

#### Reference (projection) Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `SourceProperty` | `string?` | null (uses property name as camelCase) | The source reference property name |

When used in a projection, reference includes are automatically configured on the query — no need to call `.WithReferences()` separately.

---

### QueryAggregate<T>

Marks a type as an aggregation projection for context-level `Aggregate<TProjection>()`. The generic argument `T` identifies the source collection type.

```csharp
[QueryAggregate<Product>]
public class ProductStats
{
    [Metrics(Metric.Number.Mean, Metric.Number.Sum, Metric.Number.Count, Metric.Number.Min, Metric.Number.Max)]
    public Aggregate.Number Price { get; set; }

    [Metrics(Metric.Integer.Mean, Metric.Integer.Sum, Metric.Integer.Count)]
    public Aggregate.Integer Stock { get; set; }

    [Metrics(Metric.Text.Count, Metric.Text.TopOccurrences)]
    public Aggregate.Text Category { get; set; }

    [Metrics(Metric.Boolean.TotalTrue, Metric.Boolean.PercentageTrue)]
    public Aggregate.Boolean InStock { get; set; }
}
```

#### MetricsAttribute

Use `[Metrics]` to specify which aggregate metrics to compute. The attribute supports two patterns:

**Pattern 1: Full Aggregate Type**

Use with `Aggregate.Number`, `Aggregate.Integer`, etc. types to retrieve all metrics for a property:

```csharp
[Metrics(Metric.Number.Mean, Metric.Number.Sum, Metric.Number.Count)]
public Aggregate.Number Price { get; set; }
```

**Pattern 2: Single Metric Extraction**

Extract a single metric to a scalar property by specifying the source property name:

```csharp
[Metrics("price", Metric.Number.Mean)]
public double? AveragePrice { get; set; }

[Metrics("price", Metric.Number.Count)]
public long? PriceCount { get; set; }

[Metrics("stock", Metric.Integer.Sum)]
public long? TotalStock { get; set; }

[Metrics("createdAt", Metric.Date.Min)]
public DateTime? EarliestDate { get; set; }
```

The analyzer validates that the scalar type matches the metric (e.g., `Mean` → `double?`, `Count` → `long?`).

**Naming Convention (WEAVIATE008):** When using single-metric extraction, the analyzer recommends (warning) following the naming convention: **PropertyName + MetricSuffix**. For example, `[Metrics("price", Metric.Number.Mean)]` should use property name `PriceMean` (not `AveragePrice`). This is only a warning and can be ignored if you prefer more descriptive names.

```csharp
// Recommended (follows convention):
[Metrics("price", Metric.Number.Mean)]
public double? PriceMean { get; set; }

[Metrics("price", Metric.Number.Sum)]
public double? PriceSum { get; set; }

[Metrics("stock", Metric.Integer.Count)]
public long? StockCount { get; set; }

// Also valid (analyzer warns but allows):
[Metrics("price", Metric.Number.Mean)]
public double? AveragePrice { get; set; }  // ⚠️ WEAVIATE008: Should be 'PriceMean'
```

##### Metrics Enum Types

Each aggregate type has its own enum of available metrics:

**Metric.Number** (for `Aggregate.Number`):
- `Mean`, `Sum`, `Count`, `Min`, `Max`, `Median`, `Mode`

**Metric.Integer** (for `Aggregate.Integer`):
- `Mean`, `Sum`, `Count`, `Min`, `Max`, `Median`, `Mode`

**Metric.Text** (for `Aggregate.Text`):
- `Count`, `TopOccurrences`

**Metric.Boolean** (for `Aggregate.Boolean`):
- `Count`, `TotalTrue`, `TotalFalse`, `PercentageTrue`, `PercentageFalse`

**Metric.Date** (for `Aggregate.Date`):
- `Count`, `Min`, `Max`, `Median`, `Mode`

##### Type Matching

For full aggregate types, specify multiple metrics using params syntax (comma-separated values). The analyzer enforces that the metrics enum type matches the property's aggregate type.

For single metric extraction, the analyzer enforces the following type mappings:

| Metric | Expected Scalar Type |
| --- | --- |
| `Number.Mean`, `Number.Sum`, `Number.Min`, `Number.Max`, `Number.Median`, `Number.Mode` | `double?`, `float?`, or `decimal?` |
| `Number.Count` | `long?` or `int?` |
| `Integer.Mean` | `double?` or `float?` |
| `Integer.Sum`, `Integer.Count`, `Integer.Min`, `Integer.Max`, `Integer.Median`, `Integer.Mode` | `long?` or `int?` |
| `Text.Count` | `long?` or `int?` |
| `Boolean.Count`, `Boolean.TotalTrue`, `Boolean.TotalFalse` | `long?` or `int?` |
| `Boolean.PercentageTrue`, `Boolean.PercentageFalse` | `double?` or `float?` |
| `Date.Count` | `long?` or `int?` |
| `Date.Min`, `Date.Max`, `Date.Median`, `Date.Mode` | `DateTime?` or `DateTimeOffset?` |

The `[QueryAggregate<T>]` attribute tells the context which collection to aggregate against, so the collection doesn't need to be specified explicitly.
