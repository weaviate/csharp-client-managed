using WebApi.Endpoints;
using WebApi.Models;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure port (5001 for macOS, since 5000 is used by system)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5001);
});

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Weaviate client and context
// Read hostname from environment variable (default to localhost for local dev)
var weaviateHostname = builder.Configuration.GetValue<string>("Weaviate:Hostname") ?? "localhost";
var weaviateRestPort = (ushort)builder.Configuration.GetValue<int>("Weaviate:RestPort", 8080);
var weaviateGrpcPort = (ushort)builder.Configuration.GetValue<int>("Weaviate:GrpcPort", 50051);

builder.Services.AddWeaviateLocal(
    hostname: weaviateHostname,
    restPort: weaviateRestPort,
    grpcPort: weaviateGrpcPort
);

builder.Services.AddWeaviateContext<ProductCatalogContext>(
    configureOptions: null,
    eagerMigration: true
);

// Register data seeder
builder.Services.AddHostedService<DataSeeder>();

// CORS for SvelteKit dev server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Ensure schema is ready before accepting requests
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProductCatalogContext>();
    try
    {
        app.Logger.LogInformation("Verifying collections...");
        // Migrations run automatically with eagerMigration: true
        // This just ensures context is initialized before DataSeeder runs
        await context.Client.IsReady();
        app.Logger.LogInformation("Collections ready!");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to verify collections");
        throw;
    }
}

// Development middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve static files (product images)
app.UseStaticFiles();

// Map endpoints
app.MapHealthCheck();
app.MapProductEndpoints();
app.MapReviewEndpoints();
app.MapSearchEndpoints();

await app.RunAsync();
