# Start Aspire Dashboard for OpenTelemetry
# This script starts the Aspire Dashboard via Docker for viewing agent telemetry

Write-Host "Starting Aspire Dashboard..." -ForegroundColor Green
Write-Host ""

# Check if Docker is running
try {
    docker version | Out-Null
    Write-Host "Docker is running" -ForegroundColor Green
} catch {
    Write-Host "Docker is not running or not installed" -ForegroundColor Red
    Write-Host "Please start Docker Desktop and try again" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Starting Aspire Dashboard via Docker..." -ForegroundColor Cyan

# Stop any existing Aspire Dashboard container
Write-Host "Stopping any existing Aspire Dashboard container..." -ForegroundColor Gray
docker stop aspire-dashboard-contoso 2>$null | Out-Null
docker rm aspire-dashboard-contoso 2>$null | Out-Null

# Start Aspire Dashboard in Docker daemon mode
Write-Host "Starting Aspire Dashboard container..." -ForegroundColor Green

$dockerResult = docker run -d `
    --name aspire-dashboard-contoso `
    -p 18888:18888 `
    -p 4318:18889 `
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
    --restart unless-stopped `
    mcr.microsoft.com/dotnet/aspire-dashboard:latest

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to start Aspire Dashboard container" -ForegroundColor Red
    Write-Host "Make sure Docker is running and try again" -ForegroundColor Red
    exit 1
}

Write-Host "Aspire Dashboard started successfully!" -ForegroundColor Green
Write-Host ""

# Wait for dashboard to be ready
Write-Host "Waiting for dashboard to be ready" -NoNewline -ForegroundColor Cyan
$maxWaitSeconds = 30
$waitCount = 0
$dashboardReady = $false

while (($waitCount -lt $maxWaitSeconds) -and !$dashboardReady) {
    try {
        $tcpConnection = Test-NetConnection -ComputerName localhost -Port 4318 -InformationLevel Quiet -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
        if ($tcpConnection) {
            $dashboardReady = $true
            Write-Host ""
            Write-Host "Dashboard is ready! (took $waitCount seconds)" -ForegroundColor Green
        } else {
            Write-Host "." -NoNewline -ForegroundColor Gray
            Start-Sleep -Seconds 1
            $waitCount++
        }
    } catch {
        Write-Host "." -NoNewline -ForegroundColor Gray
        Start-Sleep -Seconds 1
        $waitCount++
    }
}

if (!$dashboardReady) {
    Write-Host ""
    Write-Host "Dashboard port 4318 not responding after $maxWaitSeconds seconds" -ForegroundColor Yellow
    Write-Host "Continuing anyway - dashboard might still be starting..." -ForegroundColor Yellow
} else {
    Write-Host ""
}

# Open the dashboard in browser
Write-Host "Opening dashboard in browser..." -ForegroundColor Green
Write-Host "Dashboard URL: http://localhost:18888" -ForegroundColor Cyan
Start-Process "http://localhost:18888"

Write-Host ""
Write-Host "Aspire Dashboard is running!" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  - Dashboard UI: http://localhost:18888" -ForegroundColor White
Write-Host "  - OTLP Endpoint (HTTP): http://localhost:4318" -ForegroundColor White
Write-Host "  - Container Name: aspire-dashboard-contoso" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Environment variable already configured correctly (defaults to http://localhost:4318)" -ForegroundColor White
Write-Host "  2. Run your agent application: cd src\\backend\\ContosoBikestore.Agent.Host; dotnet run" -ForegroundColor White
Write-Host "  3. Open dashboard: http://localhost:18888" -ForegroundColor White
Write-Host ""
Write-Host "To stop the dashboard: docker stop aspire-dashboard-contoso" -ForegroundColor Gray
Write-Host "To view logs: docker logs -f aspire-dashboard-contoso" -ForegroundColor Gray
