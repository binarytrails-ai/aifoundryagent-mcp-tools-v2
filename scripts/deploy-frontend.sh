#!/bin/bash
# Deploy Next.js frontend to Azure App Service
# This script replicates the GitHub Actions workflow for manual deployment

set -e

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-contosoagent-dev}"
WEB_APP_NAME="${WEB_APP_NAME:-contosoagent-web-mlylcr}"
SKIP_BUILD=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        --web-app-name)
            WEB_APP_NAME="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Change to frontend directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FRONTEND_DIR="$SCRIPT_DIR/../src/frontend"
cd "$FRONTEND_DIR"

echo -e "\033[0;32mStarting deployment process...\033[0m"

# Step 1: Install dependencies and build (similar to GitHub Actions)
if [ "$SKIP_BUILD" = false ]; then
    echo -e "\n\033[0;36m[1/5] Installing dependencies...\033[0m"
    npm ci

    echo -e "\n\033[0;36m[2/5] Building Next.js app...\033[0m"
    npm run build
else
    echo -e "\n\033[0;33m[1/5] Skipping build (using existing build)\033[0m"
fi

# Step 2: Copy static assets (similar to GitHub Actions mv commands)
echo -e "\n\033[0;36m[3/5] Copying static assets to standalone...\033[0m"

# Copy .next/static to .next/standalone/src/frontend/.next/static
if [ -d ".next/static" ]; then
    STATIC_DEST=".next/standalone/src/frontend/.next/static"
    rm -rf "$STATIC_DEST"
    mkdir -p ".next/standalone/src/frontend/.next"
    cp -r .next/static "$STATIC_DEST"
    echo -e "\033[0;32m✓ Copied .next/static to standalone\033[0m"
else
    echo -e "\033[0;33m⚠ .next/static not found\033[0m"
fi

# Copy public to .next/standalone/src/frontend/public
if [ -d "public" ]; then
    PUBLIC_DEST=".next/standalone/src/frontend/public"
    rm -rf "$PUBLIC_DEST"
    mkdir -p ".next/standalone/src/frontend"
    cp -r public "$PUBLIC_DEST"
    echo -e "\033[0;32m✓ Copied public to standalone\033[0m"
else
    echo -e "\033[0;33m⚠ public folder not found\033[0m"
fi

# Step 3: Create deployment package (similar to GitHub Actions package step)
echo -e "\n\033[0;36m[4/5] Creating deployment package...\033[0m"

STANDALONE_DIR=".next/standalone/src/frontend"
DEPLOY_ZIP="$SCRIPT_DIR/../deploy.zip"

# Remove old zip if exists
rm -f "$DEPLOY_ZIP"

# Create zip from standalone directory
cd "$STANDALONE_DIR"
zip -r "$DEPLOY_ZIP" ./*
cd - > /dev/null

echo -e "\033[0;32m✓ Created deployment package: $DEPLOY_ZIP\033[0m"

# Step 4: Deploy to Azure Web App (similar to GitHub Actions azure/webapps-deploy@v2)
echo -e "\n\033[0;36m[5/5] Deploying to Azure App Service...\033[0m"
echo -e "\033[0;37mWeb App: $WEB_APP_NAME\033[0m"
echo -e "\033[0;37mResource Group: $RESOURCE_GROUP\033[0m"

az webapp deployment source config-zip \
    --name "$WEB_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --src "$DEPLOY_ZIP"

echo -e "\n\033[0;32m✓ Deployment completed successfully!\033[0m"
echo -e "\033[0;36mYour app should be available at: https://$WEB_APP_NAME.azurewebsites.net\033[0m"

# Cleanup
echo -e "\n\033[0;37mCleaning up deployment package...\033[0m"
rm -f "$DEPLOY_ZIP"

echo -e "\n\033[0;32mDone! 🎉\033[0m"
