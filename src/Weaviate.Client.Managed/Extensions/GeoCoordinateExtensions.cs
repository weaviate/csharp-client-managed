using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Extensions;

/// <summary>
/// Extension methods for <see cref="GeoCoordinate"/> enabling geo-range filtering
/// via LINQ expression trees in <c>.Where()</c> queries.
/// </summary>
public static class GeoCoordinateExtensions
{
    /// <summary>
    /// Marker method used in LINQ <c>.Where()</c> expressions to filter by geo range.
    /// This method is never called at runtime — it is intercepted by
    /// <see cref="Query.ExpressionToFilterConverter"/> and converted to a Weaviate
    /// <c>WithinGeoRange</c> filter.
    /// </summary>
    /// <param name="coordinate">The geo coordinate property on the entity.</param>
    /// <param name="latitude">Center latitude in degrees.</param>
    /// <param name="longitude">Center longitude in degrees.</param>
    /// <param name="distance">Search radius in metres.</param>
    /// <returns>Always throws; only meaningful inside a query expression tree.</returns>
    /// <example>
    /// <code>
    /// var results = await context.Stores.Query()
    ///     .Where(s => s.Location.IsWithinGeoRange(52.52f, 13.405f, 5000f))
    ///     .Execute();
    /// </code>
    /// </example>
    public static bool IsWithinGeoRange(
        this GeoCoordinate coordinate,
        float latitude,
        float longitude,
        float distance
    ) =>
        throw new InvalidOperationException(
            "IsWithinGeoRange is a query marker and must only be used inside a .Where() expression."
        );

    /// <summary>
    /// Marker method used in LINQ <c>.Where()</c> expressions to filter by geo range.
    /// This method is never called at runtime — it is intercepted by
    /// <see cref="Query.ExpressionToFilterConverter"/> and converted to a Weaviate
    /// <c>WithinGeoRange</c> filter.
    /// </summary>
    /// <param name="coordinate">The geo coordinate property on the entity.</param>
    /// <param name="constraint">The geo constraint (latitude, longitude, distance).</param>
    /// <returns>Always throws; only meaningful inside a query expression tree.</returns>
    /// <example>
    /// <code>
    /// var area = new GeoCoordinateConstraint(52.52f, 13.405f, 5000f);
    /// var results = await context.Stores.Query()
    ///     .Where(s => s.Location.IsWithinGeoRange(area))
    ///     .Execute();
    /// </code>
    /// </example>
    public static bool IsWithinGeoRange(
        this GeoCoordinate coordinate,
        GeoCoordinateConstraint constraint
    ) =>
        throw new InvalidOperationException(
            "IsWithinGeoRange is a query marker and must only be used inside a .Where() expression."
        );
}
