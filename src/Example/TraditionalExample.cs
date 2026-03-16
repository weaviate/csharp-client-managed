using System.Text.Json;
using Weaviate.Client;
using Weaviate.Client.Managed.Context;
using Weaviate.Client.Managed.Extensions;
using Weaviate.Client.Managed.Query;
using Weaviate.Client.Models;

namespace Example;

/// <summary>
/// Traditional example showing WeaviateContext operations without dependency injection.
/// Demonstrates context-level API with comparisons to core client for batch insert, queries,
/// near vector search, BM25, iterator, and CRUD operations.
/// </summary>
public class TraditionalExample
{
    private record CatDataWithVectors(float[] Vector, Cat Data);

    static async Task<List<CatDataWithVectors>> GetCatsAsync(string filename)
    {
        try
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine($"File not found: {filename}");
                return []; // Return an empty list if the file doesn't exist
            }

            using var fs = new FileStream(
                filename,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true
            );

            // Deserialize directly from the stream for better performance, especially with large files
            var data = await JsonSerializer.DeserializeAsync<List<CatDataWithVectors>>(fs) ?? [];

            return data;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error deserializing JSON: {ex.Message}");
            return []; // Return an empty list on deserialization error
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            return []; // Return an empty list on any other error
        }
    }

    public static async Task Run()
    {
        Console.WriteLine("=== Traditional Example ===\n");

        // Read 250 cats from JSON file and unmarshal into Cat class
        var cats = await GetCatsAsync("cats.json");

        // Use the C# client to store all cats with a cat class
        Console.WriteLine("Cats to store: " + cats.Count);

        var client = await Connect.Local();
        var context = new CatContext(client);

        // Delete any existing "cat" class
        try
        {
            await context.Client.Collections.Delete("Cat");
            Console.WriteLine("Deleted existing 'Cat' collection");
        }
        catch (Exception e)
        {
            Console.WriteLine($"No existing Cat collection to delete: {e.Message}");
        }

        // Ensure collection exists and is migrated
        await context.Set<Cat>().Migrate();

        await foreach (var c in context.Client.Collections.List())
        {
            Console.WriteLine($"Collection: {c.Name}");
        }

        // Batch Insertion Demo - context-level operation
        var requests = cats.Select(c => c.Data with { DefaultVector = c.Vector }).ToArray();

        // With core client: await client.Collections.Use<Cat>("Cat").Data.InsertMany(requests);
        await context.Insert(requests);

        // Get all objects and sum up the counter property
        // With core client: var result = await client.Collections.Use<Cat>("Cat").Query...
        var result = await context
            .Query<Cat>()
            .Limit(250)
            .WithMetadata(MetadataOptions.Score | MetadataOptions.Distance);
        var retrieved = result;
        Console.WriteLine("Cats retrieved: " + retrieved.Count());
        var sum = retrieved.Objects().Sum(c => c.Counter);

        // Demonstrate metadata injection via [MetadataProperty] attributes
        // The Cat class has Score and Distance properties marked with [MetadataProperty]
        // They are automatically populated when using WithMetadata() + Execute()
        Console.WriteLine("\n=== Metadata via [MetadataProperty] (Execute) ===");
        foreach (var cat in retrieved.Take(3))
        {
            Console.WriteLine(cat.Object.ToStringWithMetadata());
        }

        // Demonstrate ExecuteWithResult() for explicit metadata access
        // This returns QueryResult<T> with Id, Object, and Metadata properties
        Console.WriteLine("\n=== Metadata via ExecuteWithResult() ===");
        var resultWithMetadata = await context
            .Query<Cat>()
            .Limit(3)
            .WithMetadata(MetadataOptions.Score | MetadataOptions.Distance);

        foreach (var queryResult in resultWithMetadata)
        {
            Console.WriteLine(
                $"UUID: {queryResult.UUID}, Cat: {queryResult.Object.Name}, "
                    + $"Score: {queryResult.Metadata?.Score}, Distance: {queryResult.Metadata?.Distance}"
            );
        }

        // Delete object
        var firstObj = retrieved.First();
        if (firstObj.UUID is Guid id)
        {
            // With core client: await client.Collections.Use<Cat>("Cat").Data.DeleteByID(id);
            await context.Delete<Cat>(id);
        }

        // Query again to verify deletion
        result = await context.Query<Cat>().Limit(5);
        retrieved = result.ToList();
        Console.WriteLine("Cats retrieved after deletion: " + retrieved.Count());

        Console.WriteLine("Querying Neighboring Cats: [20,21,22]");

        float[] vector1 = [20f, 21f, 22f];

        var nearVectorResults = await context
            .Query<Cat>()
            .NearVector(vectorValues: vector1, distance: 0.5f)
            .Limit(5)
            .WithMetadata(MetadataOptions.Score | MetadataOptions.Distance);

        foreach (var queryResult in nearVectorResults)
        {
            Console.WriteLine(queryResult.Object);
        }

        Console.WriteLine();
        Console.WriteLine("Using collection iterator:");

        // Cursor API demo - context-level operation
        // With core client: var objects = client.Collections.Use<Cat>("Cat").Iterator();
        var objects = context.Iterator<Cat>();
        var sumWithIterator = await objects.SumAsync(c => c.Counter);

        // Print all cats found
        foreach (var cat in await objects.OrderBy(x => x.Counter).ToListAsync())
        {
            Console.WriteLine(cat);
        }

        Console.WriteLine($"Sum of counter on cats: {sumWithIterator}");

        var sphynxResults = await context
            .Query<Cat>()
            .BM25(query: "Sphynx")
            .WithMetadata(MetadataOptions.Score)
            .Execute();

        Console.WriteLine();
        Console.WriteLine("Querying Cat Breed: Sphynx");
        foreach (var queryResult in sphynxResults)
        {
            Console.WriteLine(queryResult.Object);
        }

        await context.DisposeAsync();
        client.Dispose();
    }
}
