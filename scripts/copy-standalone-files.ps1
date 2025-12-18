# Copy static files to standalone output for Next.js deployment
# Next.js standalone mode requires static assets and public folder to be copied
# to the standalone directory for proper serving
$staticSrc = ".next/static"
$staticDest = ".next/standalone/src/frontend/.next/static"
$publicSrc = "public"
$publicDest = ".next/standalone/src/frontend/public"

if (Test-Path $staticSrc) {
    Copy-Item -Path $staticSrc -Destination $staticDest -Recurse -Force
    Write-Host "Copied .next/static to standalone"
} else {
    Write-Warning ".next/static not found"
}

if (Test-Path $publicSrc) {
    Copy-Item -Path $publicSrc -Destination $publicDest -Recurse -Force
    Write-Host "Copied public to standalone"
} else {
    Write-Warning "public folder not found"
}

Write-Host "Static files copied to standalone successfully"
