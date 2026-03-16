namespace WebApi.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthCheck(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .WithName("Health")
            .WithTags("Health");
    }
}
