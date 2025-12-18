#!/usr/bin/env pwsh
# Deploy Next.js frontend to Azure App Service
# This script replicates the GitHub Actions workflow for manual deployment

param(
    [string]$ResourceGroup = "rg-contosoagent-dev",
    [string]$WebAppName = "contosoagent-web-mlylcr",
    [switch]$SkipBuild
)

# Change to frontend directory
$frontendDir = Join-Path $PSScriptRoot ".." "src" "frontend"
Set-Location $frontendDir

Write-Host "Starting deployment process..." -ForegroundColor Green

# Step 1: Install dependencies and build (similar to GitHub Actions)
if (-not $SkipBuild) {
    Write-Host "`n[1/5] Installing dependencies..." -ForegroundColor Cyan
    npm ci
    if ($LASTEXITCODE -ne 0) {
        Write-Error "npm ci failed"
        exit 1
    }

    Write-Host "`n[2/5] Building Next.js app..." -ForegroundColor Cyan
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "npm build failed"
        exit 1
    }
} else {
    Write-Host "`n[1/5] Skipping build (using existing build)" -ForegroundColor Yellow
}

Write-Host "`n[3/5] Copying static assets to standalone..." -ForegroundColor Cyan

# Copy .next/static to .next/standalone/src/frontend/.next/static
if (Test-Path ".next/static") {
    $staticDest = ".next/standalone/src/frontend/.next/static"
    if (Test-Path $staticDest) {
        Remove-Item -Path $staticDest -Recurse -Force
    }
    Copy-Item -Path ".next/static" -Destination $staticDest -Recurse -Force
    Write-Host "✓ Copied .next/static to standalone" -ForegroundColor Green
} else {
    Write-Warning ".next/static not found"
}

# Copy public to .next/standalone/src/frontend/public
if (Test-Path "public") {
    $publicDest = ".next/standalone/src/frontend/public"
    if (Test-Path $publicDest) {
        Remove-Item -Path $publicDest -Recurse -Force
    }
    Copy-Item -Path "public" -Destination $publicDest -Recurse -Force
    Write-Host "✓ Copied public to standalone" -ForegroundColor Green
} else {
    Write-Warning "public folder not found"
}

# Step 3: Create deployment package (similar to GitHub Actions package step)
Write-Host "`n[4/5] Creating deployment package..." -ForegroundColor Cyan

$standaloneDir = ".next/standalone/src/frontend"
$deployZip = Join-Path $PSScriptRoot ".." "deploy.zip"

# Remove old zip if exists
if (Test-Path $deployZip) {
    Remove-Item -Path $deployZip -Force
}

# Create zip from standalone directory
Push-Location $standaloneDir
Compress-Archive -Path * -DestinationPath $deployZip -Force
Pop-Location

Write-Host "✓ Created deployment package: $deployZip" -ForegroundColor Green

# Step 4: Deploy to Azure Web App (similar to GitHub Actions azure/webapps-deploy@v2)
Write-Host "`n[5/5] Deploying to Azure App Service..." -ForegroundColor Cyan
Write-Host "Web App: $WebAppName" -ForegroundColor Gray
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Gray

az webapp deployment source config-zip `
    --name $WebAppName `
    --resource-group $ResourceGroup `
    --src $deployZip

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed"
    exit 1
}

Write-Host "`n✓ Deployment completed successfully!" -ForegroundColor Green
Write-Host "Your app should be available at: https://$WebAppName.azurewebsites.net" -ForegroundColor Cyan

# Cleanup
Write-Host "`nCleaning up deployment package..." -ForegroundColor Gray
if (Test-Path $deployZip) {
    Remove-Item -Path $deployZip -Force
}

Write-Host "`nDone! 🎉" -ForegroundColor Green
