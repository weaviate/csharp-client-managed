#!/bin/bash
set -e

echo "🔨 Building backend..."
cd WebApi
dotnet publish -c Release -o bin/publish
cd ..

echo "📦 Building frontend..."
cd WebApp
npm ci
npm run build
cd ..

echo "✅ Build complete! Now run:"
echo "   docker compose -f docker-compose.dev.yml up"
echo ""
echo "Or use the single-command script:"
echo "   ./start.sh --docker"
