#!/bin/bash
# Alternative to docker-compose: Use plain docker run commands
# This approach gives you more control and doesn't require docker-compose

set -e

echo "🚀 Starting Weaviate services with docker run..."

# Create network
docker network create weaviate-demo 2>/dev/null || true

# Start text2vec-transformers
echo "📊 Starting text2vec-transformers..."
docker run -d \
  --name weaviate-transformers \
  --network weaviate-demo \
  -e ENABLE_CUDA=0 \
  cr.weaviate.io/semitechnologies/transformers-inference:sentence-transformers-all-MiniLM-L6-v2

# Start Weaviate
echo "🗄️  Starting Weaviate..."
docker run -d \
  --name weaviate \
  --network weaviate-demo \
  -p 8080:8080 \
  -p 50051:50051 \
  -e QUERY_DEFAULTS_LIMIT=25 \
  -e AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED=true \
  -e PERSISTENCE_DATA_PATH=/var/lib/weaviate \
  -e DEFAULT_VECTORIZER_MODULE=text2vec-transformers \
  -e ENABLE_MODULES=text2vec-transformers \
  -e TRANSFORMERS_INFERENCE_API=http://weaviate-transformers:8080 \
  -e CLUSTER_HOSTNAME=node1 \
  -v weaviate-data:/var/lib/weaviate \
  cr.weaviate.io/semitechnologies/weaviate:1.34.0

echo "✅ Services started!"
echo ""
echo "Wait ~15 seconds for Weaviate to be ready, then:"
echo "  curl http://localhost:8080/v1/.well-known/ready"
echo ""
echo "To stop:"
echo "  docker stop weaviate weaviate-transformers"
echo "  docker rm weaviate weaviate-transformers"
