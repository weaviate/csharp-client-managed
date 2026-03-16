# Schema Migrations

Safe schema evolution with breaking change detection.

## Overview

Managed provides schema migration capabilities:
- **Compare** your C# type against the existing Weaviate collection
- **Detect** what changes are needed
- **Classify** changes as safe or breaking
- **Apply** safe changes automatically
- **Block** breaking changes unless explicitly allowed

---

## Migration Workflow

### 1. Check Migration (Dry Run)

```csharp
// Via ManagedCollection
var plan = await collection.CheckMigrate();

// Via CollectionSet
var plan = await context.Products.CheckMigrate();

Console.WriteLine($"Has changes: {plan.HasChanges}");
Console.WriteLine($"Has breaking changes: {plan.HasBreakingChanges}");

foreach (var change in plan.Changes)
{
    var icon = change.IsBreaking ? "⚠️" : "✓";
    Console.WriteLine($"{icon} {change.Type}: {change.Description}");
}
```

### 2. Review Changes

Examine the migration plan before executing:

```csharp
if (plan.HasBreakingChanges)
{
    Console.WriteLine("WARNING: The following breaking changes were detected:");
    foreach (var change in plan.Changes.Where(c => c.IsBreaking))
    {
        Console.WriteLine($"  - {change.Description}");
    }

    Console.Write("Continue? (y/n): ");
    if (Console.ReadLine() != "y")
        return;
}
```

### 3. Execute Migration

```csharp
// Safe migration (default)
await collection.Migrate();

// Allow breaking changes
await collection.Migrate(allowBreakingChanges: true);

// Skip the check step (faster but less safe)
await collection.Migrate(checkFirst: false);
```

### 4. Context-Wide Migration

When using `WeaviateContext`, you can check and migrate all registered `CollectionSet<T>` types:

```csharp
// Check all pending migrations (includes orphan detection)
var pending = await context.GetPendingMigrations();

// Migrate all collections (safe changes only)
await context.Migrate();

// Allow breaking schema changes
await context.Migrate(allowBreakingChanges: true);

// Also delete orphaned collections (see below)
await context.Migrate(destructive: true);
```

### 5. Orphan Collection Detection

`GetPendingMigrations()` automatically detects **orphaned collections** — collections that exist on the Weaviate server but are not registered as `CollectionSet<T>` properties in the context.

```csharp
var pending = await context.GetPendingMigrations();

foreach (var (name, plan) in pending)
{
    if (plan.IsOrphaned)
    {
        Console.WriteLine($"Orphaned: {name} (exists on server, not in context)");
    }
    else if (plan.IsCreate)
    {
        Console.WriteLine($"New: {name} (will be created)");
    }
    else if (plan.HasChanges)
    {
        Console.WriteLine($"Modified: {name} ({plan.Changes.Count} changes)");
    }
}
```

Orphaned collections are reported as breaking changes (they are never deleted by default). To remove them, use the `destructive` flag:

```csharp
// Deletes orphaned server collections not registered in the context
await context.Migrate(destructive: true);
```

This is useful for cleaning up test or development environments. Use with caution in production.

---

## Safe vs Breaking Changes

### Safe Changes ✓

Automatically applied without confirmation:

| Change | Description |
|--------|-------------|
| Add property | New `[Property]` attribute on type |
| Add vector | New `[Vector<T>]` attribute on type |
| Add reference | New `[Reference]` attribute on type |
| Update description | Change property/collection description |
| Update mutable settings | AutoTenantCreation, AutoTenantActivation |
| Add index | Enable Filterable, Searchable, RangeFilters |

### Breaking Changes ⚠️

Blocked by default, require `allowBreakingChanges: true`:

| Change | Impact |
|--------|--------|
| Remove property | Data loss - existing data deleted |
| Change data type | Incompatible - may fail or lose data |
| Remove vector | Vector data deleted |
| Change vector config | May require re-indexing |
| Change immutable settings | Sharding, replication factor |
| Disable multi-tenancy | Not supported after creation |
| Orphaned collection | Server collection not in context (requires `destructive: true` to delete) |

---

## Change Detection

### Property Changes

```csharp
// Original
[Property(DataType.Text)]
public string Title { get; set; }

// Changed to int - BREAKING
[Property(DataType.Int)]
public int Title { get; set; }

// Renamed property - BREAKING (remove + add)
[Property(Name = "article_title")]
public string Title { get; set; }
```

### Vector Changes

```csharp
// Original
[Vector<Vectorizer.Text2VecOpenAI>(Model = "text-embedding-ada-002")]
public float[]? Embedding { get; set; }

// Changed model - may be BREAKING depending on dimensions
[Vector<Vectorizer.Text2VecOpenAI>(Model = "text-embedding-3-small")]
public float[]? Embedding { get; set; }

// Changed vectorizer - BREAKING
[Vector<Vectorizer.Text2VecCohere>]
public float[]? Embedding { get; set; }
```

### Index Changes

```csharp
// Original
[Property]
[Index(Filterable = true)]
public string Name { get; set; }

// Added searchable - SAFE
[Property]
[Index(Filterable = true, Searchable = true)]
public string Name { get; set; }

// Removed filterable - may be BREAKING for existing queries
[Property]
[Index(Searchable = true)]
public string Name { get; set; }
```

---

## Migration Strategies

### Development

Frequent iteration, data can be recreated:

```csharp
// Delete and recreate on breaking changes
var plan = await collection.CheckMigrate();
if (plan.HasBreakingChanges)
{
    await collection.DeleteCollection();
    await client.Collections.CreateManaged<MyType>();
}
else
{
    await collection.Migrate();
}
```

### Staging

Test migrations before production:

```csharp
// Always check first, review output
var plan = await collection.CheckMigrate();
LogMigrationPlan(plan);

if (!plan.HasBreakingChanges)
{
    await collection.Migrate();
}
else
{
    throw new InvalidOperationException(
        "Breaking changes require manual review");
}
```

### Production

Conservative approach:

```csharp
public async Task MigrateProductionAsync(ManagedCollection<T> collection)
{
    var plan = await collection.CheckMigrate();

    if (!plan.HasChanges)
    {
        _logger.LogInformation("No migration needed");
        return;
    }

    if (plan.HasBreakingChanges)
    {
        _logger.LogError("Breaking changes detected - manual intervention required");
        foreach (var change in plan.Changes.Where(c => c.IsBreaking))
        {
            _logger.LogError("  Breaking: {Description}", change.Description);
        }
        throw new MigrationBlockedException(plan);
    }

    _logger.LogInformation("Applying {Count} safe changes", plan.Changes.Count);
    await collection.Migrate();
    _logger.LogInformation("Migration complete");
}
```

---

## Common Scenarios

### Adding a New Property

```csharp
// Before
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Title { get; set; }
}

// After - add new property
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Title { get; set; }

    [Property]  // NEW
    public string Summary { get; set; }
}
```

Migration: **SAFE** - property added, existing objects have null for Summary.

### Adding a New Vector

```csharp
// Before
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Title { get; set; }

    [Vector<Vectorizer.Text2VecOpenAI>]
    public float[]? TitleEmbedding { get; set; }
}

// After - add second vector
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Title { get; set; }

    [Property]  // NEW
    public string Content { get; set; }

    [Vector<Vectorizer.Text2VecOpenAI>]
    public float[]? TitleEmbedding { get; set; }

    [Vector<Vectorizer.Text2VecOpenAI>]  // NEW
    public float[]? ContentEmbedding { get; set; }
}
```

Migration: **SAFE** - new vector added, existing objects need re-vectorization.

### Changing Data Type

```csharp
// Before
[Property]
public string ViewCount { get; set; }

// After - change to int
[Property]
public int ViewCount { get; set; }
```

Migration: **BREAKING** - type incompatibility.

**Workaround**: Add new property, migrate data, remove old:

```csharp
// Step 1: Add new property
[Property]
public string ViewCount { get; set; }

[Property]
public int ViewCountInt { get; set; }  // NEW

// Step 2: Migrate data (application code)

// Step 3: Remove old, rename new
[Property(Name = "viewCount")]
public int ViewCount { get; set; }
```

### Renaming a Property

```csharp
// Before
[Property]
public string Title { get; set; }

// After - rename
[Property(Name = "headline")]
public string Title { get; set; }
```

Migration: **BREAKING** - appears as remove "title" + add "headline".

**Workaround**: Keep both during transition:

```csharp
// Transition period
[Property(Name = "title")]
public string OldTitle { get; set; }

[Property(Name = "headline")]
public string Title { get; set; }

// Migrate data, then remove OldTitle
```

---

## MigrationPlan API

```csharp
public class MigrationPlan
{
    // Collection name being migrated
    public string CollectionName { get; }

    // All detected changes
    public List<SchemaChange> Changes { get; }

    // True if any changes detected
    public bool HasChanges { get; }

    // True if this is a new collection (not yet on server)
    public bool IsCreate { get; }

    // True if all changes are safe (additive only)
    public bool IsSafe { get; }

    // True if this represents an orphaned server collection
    public bool IsOrphaned { get; }

    // Human-readable summary
    public string GetSummary();

    // Factory for orphaned collections
    public static MigrationPlan ForOrphanedCollection(string collectionName);
}

public class SchemaChange
{
    // Type of change (AddProperty, RemoveProperty, etc.)
    public SchemaChangeType ChangeType { get; }

    // Human-readable description
    public string Description { get; }

    // Whether this change is safe (additive only)
    public bool IsSafe { get; }
}

public enum SchemaChangeType
{
    AddProperty,
    AddReference,
    AddVector,
    UpdateDescription,
    UpdatePropertyDescription,
    UpdateReferenceDescription,
    UpdateInvertedIndex,
    UpdateVectorIndex,
    UpdateReplication,
    UpdateMultiTenancy,
    RemoveProperty,       // BREAKING
    RemoveReference,      // BREAKING
    RemoveVector,         // BREAKING
    ModifyPropertyType,   // BREAKING
    OrphanedCollection,   // BREAKING
    Other,
}
```

---

## Automated Migration on Startup

Using `WeaviateContext`, you can migrate all collections in a hosted service:

```csharp
public class MigrationHostedService : IHostedService
{
    private readonly BlogContext _context;
    private readonly ILogger<MigrationHostedService> _logger;

    public MigrationHostedService(BlogContext context, ILogger<MigrationHostedService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking schema migrations...");

        var pending = await _context.GetPendingMigrations();
        if (pending.Any(p => p.HasBreakingChanges))
        {
            _logger.LogError("Breaking changes detected - manual migration required");
            return;
        }

        await _context.Migrate();
        _logger.LogInformation("Migration complete");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Or per-collection using `ManagedCollection<T>`:

```csharp
public async Task MigrateProductionAsync(ManagedCollection<T> collection)
{
    var plan = await collection.CheckMigrate();

    if (!plan.HasChanges)
    {
        _logger.LogInformation("No migration needed");
        return;
    }

    if (plan.HasBreakingChanges)
    {
        _logger.LogError("Breaking changes detected - manual intervention required");
        foreach (var change in plan.Changes.Where(c => c.IsBreaking))
            _logger.LogError("  Breaking: {Description}", change.Description);
        throw new MigrationBlockedException(plan);
    }

    _logger.LogInformation("Applying {Count} safe changes", plan.Changes.Count);
    await collection.Migrate();
    _logger.LogInformation("Migration complete");
}
```
