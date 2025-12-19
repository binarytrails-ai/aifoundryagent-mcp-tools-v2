#!/bin/bash

# Start Aspire Dashboard for OpenTelemetry
# This script starts the Aspire Dashboard via Docker for viewing agent telemetry

echo -e "\033[32mStarting Aspire Dashboard...\033[0m"
echo ""

# Check if Docker is running
if ! docker version &> /dev/null; then
    echo -e "\033[31mDocker is not running or not installed\033[0m"
    echo -e "\033[31mPlease start Docker and try again\033[0m"
    exit 1
fi

echo -e "\033[32mDocker is running\033[0m"
echo ""
echo -e "\033[36mStarting Aspire Dashboard via Docker...\033[0m"

# Stop any existing Aspire Dashboard container
echo -e "\033[90mStopping any existing Aspire Dashboard container...\033[0m"
docker stop aspire-dashboard-contoso 2>/dev/null
docker rm aspire-dashboard-contoso 2>/dev/null

# Start Aspire Dashboard in Docker daemon mode
echo -e "\033[32mStarting Aspire Dashboard container...\033[0m"

docker run -d \
    --name aspire-dashboard-contoso \
    -p 18888:18888 \
    -p 4318:18889 \
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    --restart unless-stopped \
    mcr.microsoft.com/dotnet/aspire-dashboard:latest

if [ $? -ne 0 ]; then
    echo -e "\033[31mFailed to start Aspire Dashboard container\033[0m"
    echo -e "\033[31mMake sure Docker is running and try again\033[0m"
    exit 1
fi

echo -e "\033[32mAspire Dashboard started successfully!\033[0m"
echo ""

# Wait for dashboard to be ready
echo -n -e "\033[36mWaiting for dashboard to be ready\033[0m"
maxWaitSeconds=30
waitCount=0
dashboardReady=false

while [ $waitCount -lt $maxWaitSeconds ] && [ "$dashboardReady" = false ]; do
    if nc -z localhost 4318 2>/dev/null || curl -s http://localhost:4318 > /dev/null 2>&1; then
        dashboardReady=true
        echo ""
        echo -e "\033[32mDashboard is ready! (took $waitCount seconds)\033[0m"
    else
        echo -n "."
        sleep 1
        ((waitCount++))
    fi
done

if [ "$dashboardReady" = false ]; then
    echo ""
    echo -e "\033[33mDashboard port 4318 not responding after $maxWaitSeconds seconds\033[0m"
    echo -e "\033[33mContinuing anyway - dashboard might still be starting...\033[0m"
fi

echo ""
echo -e "\033[32mAspire Dashboard is running!\033[0m"
echo ""
echo -e "\033[36mConfiguration:\033[0m"
echo -e "  - Dashboard UI: \033[37mhttp://localhost:18888\033[0m"
echo -e "  - OTLP Endpoint (HTTP): \033[37mhttp://localhost:4318\033[0m"
echo -e "  - Container Name: \033[37maspire-dashboard-contoso\033[0m"
echo ""
echo -e "\033[36mNext steps:\033[0m"
echo -e "  1. Environment variable already configured correctly (defaults to http://localhost:4318)"
echo -e "  2. Run your agent application: \033[37mcd src/backend/ContosoBikestore.Agent.Host && dotnet run\033[0m"
echo -e "  3. Open dashboard: \033[37mhttp://localhost:18888\033[0m"
echo ""
echo -e "\033[90mTo stop the dashboard: docker stop aspire-dashboard-contoso\033[0m"
echo -e "\033[90mTo view logs: docker logs -f aspire-dashboard-contoso\033[0m"
echo ""
echo -e "\033[36mOpening dashboard in browser...\033[0m"

# Try to open browser (works on most systems)
if command -v xdg-open &> /dev/null; then
    xdg-open "http://localhost:18888" &> /dev/null &
elif command -v open &> /dev/null; then
    open "http://localhost:18888" &> /dev/null &
else
    echo -e "\033[33mPlease open http://localhost:18888 in your browser\033[0m"
fi
