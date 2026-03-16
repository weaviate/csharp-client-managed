#!/bin/bash
# Stop and remove Weaviate containers

echo "🛑 Stopping Weaviate services..."
docker stop weaviate weaviate-transformers 2>/dev/null || true
docker rm weaviate weaviate-transformers 2>/dev/null || true

echo "✅ Stopped and removed containers"
echo ""
echo "To also remove the network:"
echo "  docker network rm weaviate-demo"
echo ""
echo "To remove data (warning: deletes all data!):"
echo "  docker volume rm weaviate-data"
