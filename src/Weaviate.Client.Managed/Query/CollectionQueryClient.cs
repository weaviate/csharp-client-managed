using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Weaviate.Client.Internal;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Mapping;
using Weaviate.Client.Managed.Models;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using Weaviate.Client.Typed;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Query
{
    /// <summary>
    /// Fluent query builder for type-safe LINQ-style queries.
    /// Provides a declarative API for building Weaviate queries with compile-time type safety.
    /// </summary>
    /// <typeparam name="T">The entity type being queried.</typeparam>
    /// <example>
    /// <code>
    /// var results = await collection.Query
    ///     .Where(a => a.WordCount > 100)
    ///     .NearText("technology", vector: a => a.Embedding)
    ///     .Limit(10)
    ///     .Execute();
    /// </code>
    /// </example>
    public class CollectionMapperQueryClient<T>
        where T : class, new()
    {
        private readonly CollectionClient _collection;
        private readonly TypedQueryClient<T> _typedClient;

        // Eager references discovered from [Reference(Loading = Eager)] attributes
        private static readonly Lazy<List<string>> _eagerReferences = new(DiscoverEagerReferences);

        // Query state
        private Filter? _filter;
        private uint? _limit;
        private uint? _offset;
        private uint? _autoLimit;
        private Guid? _after;
        private readonly List<string> _includeVectors = new();
        private readonly List<string> _includeReferences = new();
        private readonly List<Sort> _sorts = [];
        private Rerank? _rerank;
        private AutoArray<string>? _returnProperties;
        private MetadataQuery? _returnMetadata;

        // Search state
        private SearchMode _searchMode = SearchMode.Fetch;
        private object? _searchTarget;
        private TargetVectors? _targetVectors;
        private float? _distance;
        private float? _certainty;
        private float? _alpha;
        private HybridFusion? _fusionType;
        private float? _maxVectorDistance;
        private string[]? _bm25SearchFields;
        private BM25Operator? _bm25Operator;
        private VectorSearchInput? _hybridVectorSearch;

        internal CollectionMapperQueryClient(CollectionClient collection)
        {
            _collection = collection;
            _typedClient = new TypedQueryClient<T>(collection.Query);
            ApplyConfigureSearchHook();
        }

        private static List<string> DiscoverEagerReferences()
        {
            return typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => (prop: p, attr: p.GetCustomAttribute<ReferenceAttribute>()))
                .Where(x => x.attr?.Loading == ReferenceLoadingStrategy.Eager)
                .Select(x => PropertyHelper.ToCamelCase(x.prop.Name))
                .ToList();
        }

        #region Filter Methods

        /// <summary>
        /// Filters results using a type-safe lambda expression.
        /// Multiple Where calls are combined with AND logic.
        /// </summary>
        /// <param name="predicate">The filter predicate (e.g., a => a.Age > 18).</param>
        /// <returns>The query builder for chaining.</returns>
        /// <example>
        /// <code>
        /// .Where(a => a.WordCount > 100)
        /// .Where(a => a.PublishedAt > DateTime.Now.AddDays(-7))
        /// </code>
        /// </example>
        public CollectionMapperQueryClient<T> Where(Expression<Func<T, bool>> predicate)
        {
            var filter = ExpressionToFilterConverter.Convert(predicate);
            _filter = _filter == null ? filter : Filter.AllOf(_filter, filter);
            return this;
        }

        #endregion

        #region Vector Search Methods

        /// <summary>
        /// Performs a near text search using text-to-vector conversion.
        /// </summary>
        /// <param name="text">The search text.</param>
        /// <param name="vector">Optional: specify which named vector to use.</param>
        /// <param name="certainty">Minimum certainty threshold (0-1).</param>
        /// <param name="distance">Maximum distance threshold.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> NearText(
            string text,
            Expression<Func<T, object>>? vector = null,
            float? certainty = null,
            float? distance = null
        )
        {
            _searchMode = SearchMode.NearText;
            _searchTarget = text;
            SetTargetVector(vector);
            _certainty = certainty;
            _distance = distance;
            return this;
        }

        /// <summary>
        /// Performs a near vector search using a provided vector.
        /// </summary>
        /// <param name="vectorValues">The vector to search with.</param>
        /// <param name="vector">Optional: specify which named vector to use.</param>
        /// <param name="certainty">Minimum certainty threshold (0-1).</param>
        /// <param name="distance">Maximum distance threshold.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> NearVector(
            float[] vectorValues,
            Expression<Func<T, object>>? vector = null,
            float? certainty = null,
            float? distance = null
        )
        {
            _searchMode = SearchMode.NearVector;
            _searchTarget = (VectorSearchInput)vectorValues;
            SetTargetVector(vector);
            _certainty = certainty;
            _distance = distance;
            return this;
        }

        /// <summary>
        /// Performs a near object search using an existing object's vector.
        /// </summary>
        /// <param name="objectId">The ID of the object to search near.</param>
        /// <param name="vector">Optional: specify which named vector to use.</param>
        /// <param name="certainty">Minimum certainty threshold (0-1).</param>
        /// <param name="distance">Maximum distance threshold.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> NearObject(
            Guid objectId,
            Expression<Func<T, object>>? vector = null,
            float? certainty = null,
            float? distance = null
        )
        {
            _searchMode = SearchMode.NearObject;
            _searchTarget = objectId;
            SetTargetVector(vector);
            _certainty = certainty;
            _distance = distance;
            return this;
        }

        /// <summary>
        /// Performs a hybrid search combining BM25 keyword search with vector search.
        /// At least one of query or vector must be provided.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="vector">Optional: specify which named vector to use.</param>
        /// <param name="alpha">Balance between keyword (0) and vector (1) search. Default 0.5.</param>
        /// <param name="fusionType">The fusion algorithm to combine keyword and vector results.</param>
        /// <param name="maxVectorDistance">Maximum vector distance for results.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> Hybrid(
            string? query,
            Expression<Func<T, object>>? vector = null,
            float? alpha = null,
            HybridFusion? fusionType = null,
            float? maxVectorDistance = null
        )
        {
            if (string.IsNullOrWhiteSpace(query) && vector == null)
            {
                throw new ArgumentException(
                    "At least one of 'query' or 'vector' must be provided for hybrid search."
                );
            }

            _searchMode = SearchMode.Hybrid;
            _searchTarget = query;
            SetTargetVector(vector);
            _alpha = alpha;
            _fusionType = fusionType;
            _maxVectorDistance = maxVectorDistance;
            return this;
        }

        /// <summary>
        /// Performs a BM25 keyword search.
        /// </summary>
        /// <param name="query">The search query text.</param>
        /// <param name="searchFields">Optional: property selectors to restrict the search to specific fields.</param>
        /// <param name="searchOperator">Optional: the search operator (And/Or) for multi-term queries.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> BM25(
            string query,
            Expression<Func<T, object>>[]? searchFields = null,
            BM25Operator? searchOperator = null
        )
        {
            _searchMode = SearchMode.BM25;
            _searchTarget = query;
            _bm25SearchFields = searchFields
                ?.Select(f => PropertyHelper.ToCamelCase(PropertyHelper.GetPropertyName(f)))
                .ToArray();
            _bm25Operator = searchOperator;
            return this;
        }

        /// <summary>
        /// Performs a near vector search with per-target vector data. Chain a combination method
        /// (.Sum, .Average, .ManualWeights, etc.) to supply the vectors and combination strategy.
        /// </summary>
        public CollectionMapperQueryClient<T> NearVector(
            float? certainty = null,
            float? distance = null
        )
        {
            _searchMode = SearchMode.NearVector;
            _searchTarget = null;
            _certainty = certainty;
            _distance = distance;
            return this;
        }

        /// <summary>
        /// Performs a multi-modal near media search (image, video, audio, etc.).
        /// </summary>
        /// <param name="media">A factory function to configure the media search via the builder pattern.</param>
        /// <returns>The query builder for chaining.</returns>
        /// <example>
        /// <code>
        /// .NearMedia(m => m.Image(imageBytes).Build())
        /// </code>
        /// </example>
        public CollectionMapperQueryClient<T> NearMedia(NearMediaInput.FactoryFn media)
        {
            _searchMode = SearchMode.NearMedia;
            _searchTarget = media;
            return this;
        }

        #endregion

        #region Multi-Target Vector Methods

        /// <summary>
        /// Configures multi-target vector combination using a fluent builder.
        /// </summary>
        /// <param name="configure">
        /// A callback that receives a <see cref="TargetVectorBuilder{T}"/> and returns the configured builder.
        /// </param>
        /// <returns>The query builder for chaining.</returns>
        /// <example>
        /// <code>
        /// // Named vectors (NearText, Hybrid, NearObject):
        /// query.NearText("search").VectorTargets(t => t.Sum(c => c.Embedding!, c => c.Desc!))
        ///
        /// // Per-vector (NearVector):
        /// query.NearVector().VectorTargets(t => t.Sum((c => c.Embedding!, vec1), (c => c.Desc!, vec2)))
        ///
        /// // Manual weights:
        /// query.NearText("search").VectorTargets(t => t.ManualWeights(
        ///     (c => c.Embedding!, 0.7), (c => c.Desc!, 0.3)))
        /// </code>
        /// </example>
        public CollectionMapperQueryClient<T> VectorTargets(
            Func<TargetVectorBuilder<T>, TargetVectorBuilder<T>> configure
        )
        {
            var builder = configure(new TargetVectorBuilder<T>());
            ApplyTargetVectorBuilder(builder);
            return this;
        }

        private void ApplyTargetVectorBuilder(TargetVectorBuilder<T> builder)
        {
            if (builder.Mode == TargetVectorBuilder<T>.BuildMode.NameOnly)
            {
                _targetVectors = builder.Combination switch
                {
                    VectorCombination.Sum => TargetVectors.Sum(builder.VectorNames!.ToArray()),
                    VectorCombination.Average => TargetVectors.Average(
                        builder.VectorNames!.ToArray()
                    ),
                    VectorCombination.Minimum => TargetVectors.Minimum(
                        builder.VectorNames!.ToArray()
                    ),
                    VectorCombination.ManualWeights => TargetVectors.ManualWeights(
                        builder.Weights!.ToArray()
                    ),
                    VectorCombination.RelativeScore => TargetVectors.RelativeScore(
                        builder.Weights!.ToArray()
                    ),
                    _ => throw new InvalidOperationException(
                        $"Unsupported combination: {builder.Combination}"
                    ),
                };
            }
            else
            {
                var input = builder.Combination switch
                {
                    VectorCombination.Sum when builder.PerTargetVectors != null =>
                        new VectorSearchInput.Builder().TargetVectorsSum(
                            builder.PerTargetVectors.ToArray()
                        ),
                    VectorCombination.Average when builder.PerTargetVectors != null =>
                        new VectorSearchInput.Builder().TargetVectorsAverage(
                            builder.PerTargetVectors.ToArray()
                        ),
                    VectorCombination.Minimum when builder.PerTargetVectors != null =>
                        new VectorSearchInput.Builder().TargetVectorsMinimum(
                            builder.PerTargetVectors.ToArray()
                        ),
                    VectorCombination.ManualWeights when builder.WeightedPerTargetVectors != null =>
                        new VectorSearchInput.Builder().TargetVectorsManualWeights(
                            builder
                                .WeightedPerTargetVectors.Select(w => (w.name, w.weight, w.vector))
                                .ToArray()
                        ),
                    VectorCombination.RelativeScore when builder.WeightedPerTargetVectors != null =>
                        new VectorSearchInput.Builder().TargetVectorsRelativeScore(
                            builder
                                .WeightedPerTargetVectors.Select(w => (w.name, w.weight, w.vector))
                                .ToArray()
                        ),
                    _ => throw new InvalidOperationException(
                        $"Unsupported per-vector combination: {builder.Combination}"
                    ),
                };
                SetVectorSearchInput(input);
            }
        }

        #endregion

        #region Result Control Methods

        /// <summary>
        /// Limits the number of results returned.
        /// </summary>
        /// <param name="limit">Maximum number of results.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> Limit(uint limit)
        {
            _limit = limit;
            return this;
        }

        /// <summary>
        /// Skips the first N results (pagination offset).
        /// </summary>
        /// <param name="offset">Number of results to skip.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> Offset(uint offset)
        {
            _offset = offset;
            return this;
        }

        /// <summary>
        /// Sets the autocut limit (autoLimit) for automatic result limiting based on score discontinuities.
        /// </summary>
        /// <param name="autoLimit">The autocut threshold.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> AutoLimit(uint autoLimit)
        {
            _autoLimit = autoLimit;
            return this;
        }

        /// <summary>
        /// Sets the cursor for cursor-based pagination.
        /// Used with Fetch and BM25 search modes.
        /// </summary>
        /// <param name="cursor">The UUID of the last object from the previous page.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> After(Guid cursor)
        {
            _after = cursor;
            return this;
        }

        /// <summary>
        /// Applies a reranker to the search results.
        /// </summary>
        /// <param name="rerank">The rerank configuration.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> Rerank(Rerank rerank)
        {
            _rerank = rerank;
            return this;
        }

        /// <summary>
        /// Applies a reranker to the search results using the specified property and optional query.
        /// </summary>
        /// <param name="property">The property to rerank on.</param>
        /// <param name="query">Optional query string to rerank against.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> Rerank(string property, string? query = null) =>
            Rerank(new Rerank { Property = property, Query = query });

        /// <summary>
        /// Sorts results by a property, replacing any previous sort criteria.
        /// </summary>
        /// <typeparam name="TProp">The property type.</typeparam>
        /// <param name="property">The property selector.</param>
        /// <param name="descending">Sort in descending order.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> Sort<TProp>(
            Expression<Func<T, TProp>> property,
            bool descending = false
        )
        {
            var propName = PropertyHelper.GetPropertyName(property);
            var camelName = PropertyHelper.ToCamelCase(propName);
            var sort = Weaviate.Client.Models.Sort.ByProperty(camelName);
            _sorts.Clear();
            _sorts.Add(descending ? sort.Descending() : sort.Ascending());
            return this;
        }

        /// <summary>
        /// Sets the primary sort criterion (ascending), replacing any previous sort criteria.
        /// Chain with <see cref="ThenBy{TProp}"/> or <see cref="ThenByDescending{TProp}"/> for secondary criteria.
        /// </summary>
        public CollectionMapperQueryClient<T> OrderBy<TProp>(Expression<Func<T, TProp>> property) =>
            Sort(property, descending: false);

        /// <summary>
        /// Sets the primary sort criterion (descending), replacing any previous sort criteria.
        /// Chain with <see cref="ThenBy{TProp}"/> or <see cref="ThenByDescending{TProp}"/> for secondary criteria.
        /// </summary>
        public CollectionMapperQueryClient<T> OrderByDescending<TProp>(
            Expression<Func<T, TProp>> property
        ) => Sort(property, descending: true);

        /// <summary>
        /// Appends an ascending secondary sort criterion.
        /// </summary>
        public CollectionMapperQueryClient<T> ThenBy<TProp>(Expression<Func<T, TProp>> property)
        {
            var propName = PropertyHelper.GetPropertyName(property);
            var camelName = PropertyHelper.ToCamelCase(propName);
            _sorts.Add(Weaviate.Client.Models.Sort.ByProperty(camelName).Ascending());
            return this;
        }

        /// <summary>
        /// Appends a descending secondary sort criterion.
        /// </summary>
        public CollectionMapperQueryClient<T> ThenByDescending<TProp>(
            Expression<Func<T, TProp>> property
        )
        {
            var propName = PropertyHelper.GetPropertyName(property);
            var camelName = PropertyHelper.ToCamelCase(propName);
            _sorts.Add(Weaviate.Client.Models.Sort.ByProperty(camelName).Descending());
            return this;
        }

        #endregion

        #region Include Methods

        /// <summary>
        /// Includes named vectors in the results.
        /// Vectors will be populated in the corresponding properties.
        /// </summary>
        /// <param name="vectors">The vector properties to include.</param>
        /// <returns>The query builder for chaining.</returns>
        /// <example>
        /// <code>
        /// .WithVectors(a => a.TitleEmbedding, a => a.ContentEmbedding)
        /// </code>
        /// </example>
        public CollectionMapperQueryClient<T> WithVectors(
            params Expression<Func<T, object>>[] vectors
        )
        {
            foreach (var vector in vectors)
            {
                var vectorName = GetVectorName(vector);
                if (!_includeVectors.Contains(vectorName))
                {
                    _includeVectors.Add(vectorName);
                }
            }
            return this;
        }

        /// <summary>
        /// Includes cross-references in the results.
        /// References will be expanded and populated.
        /// </summary>
        /// <param name="references">The reference properties to include.</param>
        /// <returns>The query builder for chaining.</returns>
        /// <example>
        /// <code>
        /// .WithReferences(a => a.Category, a => a.Author)
        /// </code>
        /// </example>
        public CollectionMapperQueryClient<T> WithReferences(
            params Expression<Func<T, object>>[] references
        )
        {
            foreach (var reference in references)
            {
                var refName = PropertyHelper.GetPropertyName(reference);
                var camelName = PropertyHelper.ToCamelCase(refName);
                if (!_includeReferences.Contains(camelName))
                {
                    _includeReferences.Add(camelName);
                }
            }
            return this;
        }

        /// <summary>
        /// Specifies which properties to return.
        /// If not called, all properties are returned.
        /// </summary>
        /// <param name="selector">Property selector.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> Select(Expression<Func<T, object>> selector)
        {
            var properties = PropertyHelper.GetCamelCasePropertyNames(selector);
            _returnProperties = properties.ToArray();
            return this;
        }

        /// <summary>
        /// Switches to a projected query that maps results to TProjection instead of T.
        /// The projection type can use [MapFrom], [MetadataProperty], [Vector],
        /// and [WeaviateUUID] attributes to control mapping.
        /// </summary>
        /// <typeparam name="TProjection">The projection type to map results to.</typeparam>
        /// <returns>A projected query builder.</returns>
        /// <remarks>
        /// <b>Prefer using <c>Query&lt;TProjection&gt;()</c> directly</b> instead of <c>Query().Project&lt;TProjection&gt;()</c>.
        /// This method is kept public for advanced scenarios where you need to conditionally apply projections.
        /// </remarks>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
        public ProjectedQueryClient<T, TProjection> Project<TProjection>()
            where TProjection : class, new()
        {
            return new ProjectedQueryClient<T, TProjection>(this);
        }

        /// <summary>
        /// Sets the return properties directly from camelCase property names.
        /// Used internally by <see cref="ProjectedQueryClient{T, TProjection}"/> for auto-configuration.
        /// </summary>
        internal void SetReturnProperties(IEnumerable<string> camelCasePropertyNames)
        {
            _returnProperties = camelCasePropertyNames.ToArray();
        }

        /// <summary>
        /// Adds reference names to include in results.
        /// Used internally by <see cref="ProjectedQueryClient{T, TProjection}"/> for auto-configuration.
        /// </summary>
        internal void AddIncludeReferences(IEnumerable<string> camelCaseReferenceNames)
        {
            foreach (var name in camelCaseReferenceNames)
            {
                if (!_includeReferences.Contains(name))
                {
                    _includeReferences.Add(name);
                }
            }
        }

        /// <summary>
        /// Adds vector names to include in results.
        /// Used internally by <see cref="ProjectedQueryClient{T, TProjection}"/> for auto-configuration.
        /// </summary>
        internal void AddIncludeVectors(IEnumerable<string> vectorNames)
        {
            foreach (var name in vectorNames)
            {
                if (!_includeVectors.Contains(name))
                {
                    _includeVectors.Add(name);
                }
            }
        }

        /// <summary>
        /// Includes metadata in results (distance, certainty, creation time, etc.).
        /// </summary>
        /// <param name="metadata">Metadata options to include.</param>
        /// <returns>The query builder for chaining.</returns>
        public CollectionMapperQueryClient<T> WithMetadata(MetadataQuery metadata)
        {
            _returnMetadata = metadata;
            return this;
        }

        #endregion

        #region Apply*Config Methods (used by ProjectedQueryClient for ConfigureXxx discovery)

        /// <summary>
        /// Applies a <see cref="NearTextConfig"/> overlay if the current search mode is NearText.
        /// </summary>
        internal void ApplyNearTextConfig(Func<NearTextConfig, NearTextConfig>? configure)
        {
            if (configure == null || _searchMode != SearchMode.NearText)
                return;

            var merged = configure(
                new NearTextConfig { Certainty = _certainty, Distance = _distance }
            );
            _certainty = merged.Certainty;
            _distance = merged.Distance;
        }

        /// <summary>
        /// Applies a <see cref="NearVectorConfig"/> overlay if the current search mode is NearVector.
        /// </summary>
        internal void ApplyNearVectorConfig(Func<NearVectorConfig, NearVectorConfig>? configure)
        {
            if (configure == null || _searchMode != SearchMode.NearVector)
                return;

            var merged = configure(
                new NearVectorConfig { Certainty = _certainty, Distance = _distance }
            );
            _certainty = merged.Certainty;
            _distance = merged.Distance;
        }

        /// <summary>
        /// Applies a <see cref="HybridConfig"/> overlay if the current search mode is Hybrid.
        /// </summary>
        internal void ApplyHybridConfig(Func<HybridConfig, HybridConfig>? configure)
        {
            if (configure == null || _searchMode != SearchMode.Hybrid)
                return;

            var merged = configure(
                new HybridConfig
                {
                    Alpha = _alpha,
                    FusionType = _fusionType,
                    MaxVectorDistance = _maxVectorDistance,
                }
            );
            _alpha = merged.Alpha;
            _fusionType = merged.FusionType;
            _maxVectorDistance = merged.MaxVectorDistance;
        }

        /// <summary>
        /// Applies a <see cref="NearObjectConfig"/> overlay if the current search mode is NearObject.
        /// </summary>
        internal void ApplyNearObjectConfig(Func<NearObjectConfig, NearObjectConfig>? configure)
        {
            if (configure == null || _searchMode != SearchMode.NearObject)
                return;

            var merged = configure(
                new NearObjectConfig { Certainty = _certainty, Distance = _distance }
            );
            _certainty = merged.Certainty;
            _distance = merged.Distance;
        }

        /// <summary>
        /// Applies a <see cref="NearMediaConfig"/> overlay if the current search mode is NearMedia.
        /// </summary>
        internal void ApplyNearMediaConfig(Func<NearMediaConfig, NearMediaConfig>? configure)
        {
            if (configure == null || _searchMode != SearchMode.NearMedia)
                return;

            var merged = configure(
                new NearMediaConfig { Certainty = _certainty, Distance = _distance }
            );
            _certainty = merged.Certainty;
            _distance = merged.Distance;
        }

        /// <summary>
        /// If T defines static ConfigureSearch(QueryConfig&lt;T&gt;), invoke it to apply entity-level query defaults.
        /// This provides the lowest precedence configuration that can be overridden by projections or explicit calls.
        /// </summary>
        private void ApplyConfigureSearchHook()
        {
            var method = typeof(T).GetMethod(
                "ConfigureSearch",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(QueryConfig<T>) },
                null
            );
            if (method != null && method.ReturnType == typeof(void))
            {
                var config = new QueryConfig<T>(this);
                method.Invoke(null, new object[] { config });
            }
        }

        #endregion

        #region Execution Methods

        /// <summary>
        /// Executes the query and returns results wrapped in <see cref="QueryResult{T}"/>.
        /// Each result contains the fully-populated entity, its ID, and query metadata (if requested).
        /// Use <see cref="QueryResultExtensions.Objects{T}"/> to extract just the entities if metadata is not needed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumerable of QueryResult objects containing the entity, ID, and metadata.</returns>
        /// <example>
        /// <code>
        /// // Get full results with metadata
        /// var results = await collection.Query
        ///     .NearText("fluffy cats")
        ///     .WithMetadata(MetadataQuery.Score | MetadataQuery.Distance)
        ///     .Execute();
        ///
        /// foreach (var result in results)
        /// {
        ///     Console.WriteLine($"{result.Object.Name}: Score={result.Metadata?.Score}");
        /// }
        ///
        /// // Or extract just objects when metadata not needed
        /// var objects = (await collection.Query().Execute()).Objects();
        /// </code>
        /// </example>
        public async Task<IEnumerable<QueryResult<T>>> Execute(
            CancellationToken cancellationToken = default
        )
        {
            var result = await ExecuteQueryAsync(cancellationToken);

            return result.Objects.Select(wo => new QueryResult<T>
            {
                UUID = wo.UUID,
                Object = ManagedObjectMapper.FromWeaviateObject(wo),
                Metadata = wo.Metadata,
            });
        }

        /// <summary>
        /// Makes the query directly awaitable without calling <c>Execute()</c>.
        /// This is syntactic sugar that allows <c>await collection.Query()...</c> instead of
        /// <c>await collection.Query()...Execute()</c>.
        /// </summary>
        /// <example>
        /// <code>
        /// // These are equivalent:
        /// var results = await collection.Query().Limit(10).Execute();
        /// var results = await collection.Query().Limit(10);
        /// </code>
        /// </example>
        public TaskAwaiter<IEnumerable<QueryResult<T>>> GetAwaiter() =>
            Execute(CancellationToken.None).GetAwaiter();

        /// <summary>
        /// Switches to generative (RAG) mode. The query built so far is used as the search,
        /// and the specified prompts are sent to the generative AI model alongside the results.
        /// </summary>
        /// <param name="singlePrompt">Optional per-object prompt. Applied to each search result individually.</param>
        /// <param name="groupedTask">Optional grouped task. Applied to the entire result set as context.</param>
        /// <param name="provider">Optional generative provider override. If not set, the collection's configured module is used.</param>
        /// <returns>A generative query executor for further configuration and execution.</returns>
        /// <example>
        /// <code>
        /// var results = await collection.Query
        ///     .NearText("wireless mouse")
        ///     .Limit(10)
        ///     .Generate(singlePrompt: "Describe this product in one sentence")
        ///     .Execute();
        /// </code>
        /// </example>
        public GenerativeQueryExecutor<T> Generate(
            SinglePrompt? singlePrompt = null,
            GroupedTask? groupedTask = null,
            GenerativeProvider? provider = null
        )
        {
            return new GenerativeQueryExecutor<T>(this, singlePrompt, groupedTask, provider);
        }

        /// <summary>
        /// Returns a <see cref="GroupByQueryExecutor{T}"/> that buckets results by the specified
        /// property value. Call <see cref="GroupByQueryExecutor{T}.Execute"/> to run the query.
        /// </summary>
        /// <typeparam name="TProp">The type of the property to group by.</typeparam>
        /// <param name="property">Expression selecting the property to group on.</param>
        /// <param name="numberOfGroups">Maximum number of groups to return.</param>
        /// <param name="objectsPerGroup">Maximum objects returned per group.</param>
        /// <example>
        /// <code>
        /// var response = await context.Products.Query()
        ///     .NearText("laptop")
        ///     .Where(p => p.InStock)
        ///     .GroupBy(p => p.Category, numberOfGroups: 5, objectsPerGroup: 3)
        ///     .Execute();
        ///
        /// foreach (var group in response.Groups.Values)
        ///     Console.WriteLine($"{group.Name}: {group.Count} objects");
        /// </code>
        /// </example>
        public GroupByQueryExecutor<T> GroupBy<TProp>(
            Expression<Func<T, TProp>> property,
            uint numberOfGroups,
            uint objectsPerGroup
        )
        {
            var name = PropertyHelper.ToCamelCase(PropertyHelper.GetPropertyName(property));
            var request = new GroupByRequest(name)
            {
                NumberOfGroups = numberOfGroups,
                ObjectsPerGroup = objectsPerGroup,
            };
            return new GroupByQueryExecutor<T>(this, request);
        }

        #endregion

        #region Internal Helper Methods

        /// <summary>
        /// Executes the query based on the search mode.
        /// Internal for use by <see cref="ProjectedQueryClient{T, TProjection}"/>.
        /// </summary>
        internal async Task<
            WeaviateResult<Weaviate.Client.Models.Typed.WeaviateObject<T>>
        > ExecuteQueryAsync(CancellationToken cancellationToken)
        {
            // Build references list (merge eager + explicit)
            var allReferences = MergeReferences();
            IList<QueryReference>? queryReferences =
                allReferences.Count > 0
                    ? allReferences.Select(name => new QueryReference(name)).ToList()
                    : null;

            // Build vector include query
            VectorQuery? vectorInclude =
                _includeVectors.Count > 0 ? new VectorQuery(_includeVectors) : null;

            return _searchMode switch
            {
                SearchMode.Fetch => await _typedClient.FetchObjects(
                    after: _after,
                    limit: _limit,
                    offset: _offset,
                    filters: _filter,
                    sort: _sorts.Count > 0 ? (AutoArray<Sort>?)_sorts.ToArray() : null,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearText => await _typedClient.NearText(
                    query: new NearTextInput(
                        (string)_searchTarget!,
                        Certainty: _certainty,
                        Distance: _distance,
                        TargetVectors: _targetVectors
                    ),
                    filters: _filter,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearVector => await _typedClient.NearVector(
                    vectors: BuildNearVectorInput(),
                    filters: _filter,
                    certainty: _certainty,
                    distance: _distance,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.Hybrid => await _typedClient.Hybrid(
                    query: (string?)_searchTarget,
                    vectors: BuildHybridVectorInput(),
                    alpha: _alpha,
                    fusionType: _fusionType,
                    maxVectorDistance: _maxVectorDistance,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    filters: _filter,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.BM25 => await _typedClient.BM25(
                    query: (string)_searchTarget!,
                    searchFields: _bm25SearchFields,
                    filters: _filter,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    searchOperator: _bm25Operator,
                    rerank: _rerank,
                    after: _after,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearObject => await _typedClient.NearObject(
                    nearObject: (Guid)_searchTarget!,
                    certainty: _certainty,
                    distance: _distance,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    filters: _filter,
                    rerank: _rerank,
                    targets: _targetVectors != null ? b => _targetVectors : null,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearMedia => await _typedClient.NearMedia(
                    media: (NearMediaInput.FactoryFn)_searchTarget!,
                    filters: _filter,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                _ => throw new NotSupportedException($"Search mode {_searchMode} is not supported"),
            };
        }

        /// <summary>
        /// Fetches a single object by its UUID.
        /// This is the correct way to retrieve an object by ID (not NearObject which does vector similarity).
        /// </summary>
        /// <param name="id">The UUID of the object to fetch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The object if found, null otherwise.</returns>
        public async Task<T?> Find(Guid id, CancellationToken cancellationToken = default)
        {
            var references = MergeReferences();
            var queryReferences = references.Select(r => new QueryReference(r)).ToList();

            var vectorInclude =
                _includeVectors.Count > 0 ? new VectorQuery(_includeVectors.ToArray()) : null;

            var result = await _typedClient.FetchObjectByID(
                uuid: id,
                returnProperties: _returnProperties,
                returnReferences: queryReferences,
                returnMetadata: _returnMetadata,
                includeVectors: vectorInclude,
                cancellationToken: cancellationToken
            );

            if (result == null)
                return null;

            return ManagedObjectMapper.FromWeaviateObject(result);
        }

        /// <summary>
        /// Executes a generative query based on the search mode with additional generative params.
        /// Internal for use by <see cref="GenerativeQueryExecutor{T}"/>.
        /// </summary>
        internal async Task<GenerativeQueryResponse<T>> ExecuteGenerativeQueryAsync(
            SinglePrompt? singlePrompt,
            GroupedTask? groupedTask,
            GenerativeProvider? provider,
            CancellationToken cancellationToken
        )
        {
            var generateClient = new TypedGenerateClient<T>(_collection.Generate);

            // Build references list (merge eager + explicit)
            var allReferences = MergeReferences();
            IList<QueryReference>? queryReferences =
                allReferences.Count > 0
                    ? allReferences.Select(name => new QueryReference(name)).ToList()
                    : null;

            // Build vector include query
            VectorQuery? vectorInclude =
                _includeVectors.Count > 0 ? new VectorQuery(_includeVectors) : null;

            var result = _searchMode switch
            {
                SearchMode.Fetch => await generateClient.FetchObjects(
                    after: _after,
                    limit: _limit,
                    filters: _filter,
                    sort: _sorts.Count > 0 ? (AutoArray<Sort>?)_sorts.ToArray() : null,
                    rerank: _rerank,
                    singlePrompt: singlePrompt,
                    groupedTask: groupedTask,
                    provider: provider,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearText => await generateClient.NearText(
                    query: new NearTextInput(
                        (string)_searchTarget!,
                        Certainty: _certainty,
                        Distance: _distance,
                        TargetVectors: _targetVectors
                    ),
                    filters: _filter,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    rerank: _rerank,
                    singlePrompt: singlePrompt,
                    groupedTask: groupedTask,
                    provider: provider,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearVector => await generateClient.NearVector(
                    vectors: BuildNearVectorInput(),
                    filters: _filter,
                    certainty: _certainty,
                    distance: _distance,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    rerank: _rerank,
                    singlePrompt: singlePrompt,
                    groupedTask: groupedTask,
                    provider: provider,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.Hybrid => await generateClient.Hybrid(
                    query: (string?)_searchTarget,
                    vectors: BuildHybridVectorInput(),
                    alpha: _alpha,
                    fusionType: _fusionType,
                    maxVectorDistance: _maxVectorDistance,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    filters: _filter,
                    rerank: _rerank,
                    singlePrompt: singlePrompt,
                    groupedTask: groupedTask,
                    provider: provider,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.BM25 => await generateClient.BM25(
                    query: (string)_searchTarget!,
                    searchFields: _bm25SearchFields,
                    filters: _filter,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    rerank: _rerank,
                    singlePrompt: singlePrompt,
                    groupedTask: groupedTask,
                    provider: provider,
                    after: _after,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearObject => await generateClient.NearObject(
                    nearObject: (Guid)_searchTarget!,
                    certainty: _certainty,
                    distance: _distance,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    filters: _filter,
                    rerank: _rerank,
                    singlePrompt: singlePrompt,
                    groupedTask: groupedTask,
                    provider: provider,
                    targetVectors: _targetVectors != null ? b => _targetVectors : null,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearMedia => await generateClient.NearMedia(
                    media: (NearMediaInput.FactoryFn)_searchTarget!,
                    filters: _filter,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    rerank: _rerank,
                    singlePrompt: singlePrompt,
                    groupedTask: groupedTask,
                    provider: provider,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                _ => throw new NotSupportedException($"Search mode {_searchMode} is not supported"),
            };

            // FetchObjects can return null
            if (result == null)
            {
                return new GenerativeQueryResponse<T>
                {
                    Results = Array.Empty<GenerativeQueryResult<T>>(),
                    Generative = null,
                };
            }

            return new GenerativeQueryResponse<T>
            {
                Results = result
                    .Objects.Select(gwo => new GenerativeQueryResult<T>
                    {
                        UUID = gwo.UUID,
                        Object = ManagedObjectMapper.FromWeaviateObject(gwo),
                        Metadata = gwo.Metadata,
                        Generative = gwo.Generative,
                    })
                    .ToList(),
                Generative = result.Generative,
            };
        }

        /// <summary>
        /// Executes a group-by search query against the Weaviate collection.
        /// Internal for use by <see cref="GroupByQueryExecutor{T}"/>.
        /// </summary>
        internal async Task<GroupByResult<T>> ExecuteGroupByQueryAsync(
            GroupByRequest groupBy,
            CancellationToken cancellationToken
        )
        {
            // Build references list (merge eager + explicit)
            var allReferences = MergeReferences();
            IList<QueryReference>? queryReferences =
                allReferences.Count > 0
                    ? allReferences.Select(name => new QueryReference(name)).ToList()
                    : null;

            // Build vector include query
            VectorQuery? vectorInclude =
                _includeVectors.Count > 0 ? new VectorQuery(_includeVectors) : null;

            return _searchMode switch
            {
                SearchMode.Fetch => await _typedClient.FetchObjects(
                    groupBy: groupBy,
                    after: _after,
                    limit: _limit,
                    filters: _filter,
                    sort: _sorts.Count > 0 ? (AutoArray<Sort>?)_sorts.ToArray() : null,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearText => await _typedClient.NearText(
                    query: new NearTextInput(
                        (string)_searchTarget!,
                        Certainty: _certainty,
                        Distance: _distance,
                        TargetVectors: _targetVectors
                    ),
                    groupBy: groupBy,
                    filters: _filter,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearVector => await _typedClient.NearVector(
                    vectors: BuildNearVectorInput(),
                    groupBy: groupBy,
                    filters: _filter,
                    certainty: _certainty,
                    distance: _distance,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.Hybrid => await _typedClient.Hybrid(
                    query: (string?)_searchTarget,
                    vectors: BuildHybridVectorInput(),
                    groupBy: groupBy,
                    alpha: _alpha,
                    fusionType: _fusionType,
                    maxVectorDistance: _maxVectorDistance,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    filters: _filter,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.BM25 => await _typedClient.BM25(
                    query: (string)_searchTarget!,
                    groupBy: groupBy,
                    searchFields: _bm25SearchFields,
                    filters: _filter,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    searchOperator: _bm25Operator,
                    rerank: _rerank,
                    after: _after,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearObject => await _typedClient.NearObject(
                    nearObject: (Guid)_searchTarget!,
                    groupBy: groupBy,
                    certainty: _certainty,
                    distance: _distance,
                    limit: _limit,
                    offset: _offset,
                    autoLimit: _autoLimit,
                    filters: _filter,
                    rerank: _rerank,
                    targets: _targetVectors != null ? b => _targetVectors : null,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                SearchMode.NearMedia => await _typedClient.NearMedia(
                    media: (NearMediaInput.FactoryFn)_searchTarget!,
                    groupBy: groupBy,
                    filters: _filter,
                    autoLimit: _autoLimit,
                    limit: _limit,
                    offset: _offset,
                    rerank: _rerank,
                    returnProperties: _returnProperties,
                    returnReferences: queryReferences,
                    returnMetadata: _returnMetadata,
                    includeVectors: vectorInclude,
                    cancellationToken: cancellationToken
                ),

                _ => throw new NotSupportedException($"Search mode {_searchMode} is not supported"),
            };
        }

        /// <summary>
        /// Extracts the vector name from a property selector expression.
        /// </summary>
        private static string GetVectorName(Expression<Func<T, object>> vectorExpr)
        {
            var propName = PropertyHelper.GetPropertyName(vectorExpr);
            return PropertyHelper.ToCamelCase(propName);
        }

        /// <summary>
        /// Sets the target vector from an optional expression selector.
        /// </summary>
        private void SetTargetVector(Expression<Func<T, object>>? vector)
        {
            if (vector != null)
            {
                var vectorName = GetVectorName(vector);
                _targetVectors = new[] { vectorName };
            }
        }

        /// <summary>
        /// Routes a VectorSearchInput to the correct field based on current search mode.
        /// Used by the per-target vector combination methods (Sum, Average, etc.).
        /// </summary>
        private void SetVectorSearchInput(VectorSearchInput input)
        {
            if (_searchMode == SearchMode.NearVector)
                _searchTarget = input;
            else if (_searchMode == SearchMode.Hybrid)
                _hybridVectorSearch = input;
            else
                throw new InvalidOperationException(
                    $"Per-target vector overloads require NearVector() or Hybrid() search mode (current: {_searchMode})."
                );
        }

        /// <summary>
        /// Builds the VectorSearchInput for NearVector, combining per-target vectors with
        /// any TargetVectors combination strategy set via the target-only overloads.
        /// </summary>
        private VectorSearchInput BuildNearVectorInput()
        {
            var base_ = (VectorSearchInput?)_searchTarget;
            if (base_ == null)
                throw new InvalidOperationException(
                    "NearVector() requires vector data. Call NearVector(float[]) for a single vector, "
                        + "or NearVector().Sum((t => t.Emb, vec), ...) for per-target vectors."
                );
            return _targetVectors != null
                ? VectorSearchInput.Combine(_targetVectors, base_)
                : base_;
        }

        /// <summary>
        /// Builds the HybridVectorInput for Hybrid, respecting priority:
        /// per-target vectors > NearText-style target vectors > null (default behaviour).
        /// </summary>
        private HybridVectorInput? BuildHybridVectorInput()
        {
            if (_hybridVectorSearch != null)
                return _hybridVectorSearch;
            if (_targetVectors != null)
                return new NearTextInput((string)_searchTarget!, TargetVectors: _targetVectors);
            return null;
        }

        #endregion

        private List<string> MergeReferences()
        {
            var all = new List<string>(_eagerReferences.Value);
            foreach (var r in _includeReferences)
            {
                if (!all.Contains(r))
                    all.Add(r);
            }
            return all;
        }

        private enum SearchMode
        {
            Fetch,
            NearText,
            NearVector,
            Hybrid,
            BM25,
            NearObject,
            NearMedia,
        }
    }
}
