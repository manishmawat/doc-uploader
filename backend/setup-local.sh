#!/bin/bash
# Local development setup script for macOS/Linux

set -e

echo "🚀 Setting up Document Uploader API with Azurite..."

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker Desktop from https://www.docker.com/products/docker-desktop"
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose is not installed. It should come with Docker Desktop."
    exit 1
fi

echo "✅ Docker found"
echo "✅ Docker Compose found"

# Navigate to script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
cd "$SCRIPT_DIR"

echo ""
echo "📦 Building and starting services..."
echo "   - Document Uploader API"
echo "   - Azurite (Local Azure Storage)"
echo ""

# Build and start services
docker-compose up --build -d

# Wait for services to be ready
echo "⏳ Waiting for services to start..."
sleep 5

# Check if API is responding
MAX_RETRIES=30
RETRY_COUNT=0
API_READY=false

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if curl -s http://localhost:7071/api/documentupload/health > /dev/null 2>&1; then
        API_READY=true
        break
    fi
    RETRY_COUNT=$((RETRY_COUNT + 1))
    sleep 1
done

echo ""
if [ "$API_READY" = true ]; then
    echo "✅ Services started successfully!"
    echo ""
    echo "🎉 Your environment is ready!"
    echo ""
    echo "📍 API Endpoints:"
    echo "   Swagger/OpenAPI:  http://localhost:7071/swagger"
    echo "   Health Check:     http://localhost:7071/api/documentupload/health"
    echo "   SAS Token:        http://localhost:7071/api/documentupload/sas-token"
    echo ""
    echo "📍 Azurite Storage:"
    echo "   Blob Endpoint:    http://localhost:10000"
    echo "   Account:          devstoreaccount1"
    echo "   Container:        documents"
    echo ""
    echo "💡 Next steps:"
    echo "   1. Open http://localhost:7071/swagger in your browser"
    echo "   2. Test health endpoint (no auth required)"
    echo "   3. Download Azure Storage Explorer for blob inspection"
    echo ""
    echo "📚 Documentation:"
    echo "   - Local dev guide:  AZURITE_LOCAL_DEV.md"
    echo "   - API testing:      TESTING.md"
    echo "   - Getting started:  GETTING_STARTED.md"
    echo ""
    echo "🛑 To stop services: docker-compose down"
    echo "📊 To view logs:      docker-compose logs -f doc-uploader-api"
else
    echo "❌ Services failed to start. Check logs:"
    docker-compose logs
    exit 1
fi
