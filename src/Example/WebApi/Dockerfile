# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["weaviate/csharp-client-managed/Weaviate.slnx", "weaviate/csharp-client-managed/"]
COPY ["weaviate/csharp-client/src/Weaviate.Client/Weaviate.Client.csproj", "weaviate/csharp-client/src/Weaviate.Client/"]
COPY ["weaviate/csharp-client-managed/src/Weaviate.Client.Managed/Weaviate.Client.Managed.csproj", "weaviate/csharp-client-managed/src/Weaviate.Client.Managed/"]
COPY ["weaviate/csharp-client-managed/src/Example/WebApi/WebApi.csproj", "weaviate/csharp-client-managed/src/Example/WebApi/"]

# Restore dependencies
RUN dotnet restore "weaviate/csharp-client-managed/src/Example/WebApi/WebApi.csproj"

# Copy all source code
COPY ["weaviate/csharp-client/src/Weaviate.Client/", "weaviate/csharp-client/src/Weaviate.Client/"]
COPY ["weaviate/csharp-client-managed/src/Weaviate.Client.Managed/", "weaviate/csharp-client-managed/src/Weaviate.Client.Managed/"]
COPY ["weaviate/csharp-client-managed/src/Example/WebApi/", "weaviate/csharp-client-managed/src/Example/WebApi/"]

# Build
WORKDIR /src/weaviate/csharp-client-managed/src/Example/WebApi
RUN dotnet build "WebApi.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5001
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebApi.dll"]
