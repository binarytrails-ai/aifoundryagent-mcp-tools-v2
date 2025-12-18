#!/bin/sh
# Copy static files to standalone output for Next.js deployment
# Next.js standalone mode requires static assets and public folder to be copied
# to the standalone directory for proper serving
cp -r .next/static .next/standalone/src/frontend/.next/
cp -r public .next/standalone/src/frontend/
echo "Static files copied to standalone successfully"
