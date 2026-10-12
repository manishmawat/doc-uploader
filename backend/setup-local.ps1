#!/usr/bin/env pwsh
# Local development setup script for Windows PowerShell

Write-Host "🚀 Setting up Document Uploader API with Azurite..." -ForegroundColor Green
Write-Host ""

# Check if Docker is installed
try {
    $dockerVersion = docker --version 2>&1
    Write-Host "✅ Docker found: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker is not installed. Please install Docker Desktop from https://www.docker.com/products/docker-desktop" -ForegroundColor Red
    exit 1
}

# Check if Docker Compose is installed
try {
    $composeVersion = docker-compose --version 2>&1
    Write-Host "✅ Docker Compose found: $composeVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker Compose is not installed. It should come with Docker Desktop." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📦 Building and starting services..." -ForegroundColor Cyan
Write-Host "   - Document Uploader API" -ForegroundColor Cyan
Write-Host "   - Azurite (Local Azure Storage)" -ForegroundColor Cyan
Write-Host ""

# Build and start services
docker-compose up --build -d

# Wait for services to be ready
Write-Host "⏳ Waiting for services to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Check if API is responding
$maxRetries = 30
$retryCount = 0
$apiReady = $false

while ($retryCount -lt $maxRetries) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:7071/api/documentupload/health" -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $apiReady = $true
            break
        }
    } catch {
        # API not ready yet
    }
    $retryCount++
    Start-Sleep -Seconds 1
}

Write-Host ""
if ($apiReady) {
    Write-Host "✅ Services started successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🎉 Your environment is ready!" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "📍 API Endpoints:" -ForegroundColor Cyan
    Write-Host "   Swagger/OpenAPI:  http://localhost:7071/swagger" -ForegroundColor White
    Write-Host "   Health Check:     http://localhost:7071/api/documentupload/health" -ForegroundColor White
    Write-Host "   SAS Token:        http://localhost:7071/api/documentupload/sas-token" -ForegroundColor White
    Write-Host ""
    Write-Host "📍 Azurite Storage:" -ForegroundColor Cyan
    Write-Host "   Blob Endpoint:    http://localhost:10000" -ForegroundColor White
    Write-Host "   Account:          devstoreaccount1" -ForegroundColor White
    Write-Host "   Container:        documents" -ForegroundColor White
    Write-Host ""
    Write-Host "💡 Next steps:" -ForegroundColor Cyan
    Write-Host "   1. Open http://localhost:7071/swagger in your browser" -ForegroundColor White
    Write-Host "   2. Test health endpoint (no auth required)" -ForegroundColor White
    Write-Host "   3. Download Azure Storage Explorer for blob inspection" -ForegroundColor White
    Write-Host ""
    Write-Host "📚 Documentation:" -ForegroundColor Cyan
    Write-Host "   - Local dev guide:  AZURITE_LOCAL_DEV.md" -ForegroundColor White
    Write-Host "   - API testing:      TESTING.md" -ForegroundColor White
    Write-Host "   - Getting started:  GETTING_STARTED.md" -ForegroundColor White
    Write-Host ""
    Write-Host "🛑 To stop services: docker-compose down" -ForegroundColor Gray
    Write-Host "📊 To view logs:      docker-compose logs -f doc-uploader-api" -ForegroundColor Gray
} else {
    Write-Host "❌ Services failed to start. Check logs:" -ForegroundColor Red
    docker-compose logs
    exit 1
}
