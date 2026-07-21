#!/bin/sh
set -e

echo "Starting application as non-root..."
exec su-exec appuser dotnet track-list-api.dll
