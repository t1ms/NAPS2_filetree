#!/bin/bash
set -e

# Post-merge setup for NAPS2 (C#/.NET desktop app, cross-compiled for Windows).
# No workflows/servers run in this environment, and full `dotnet restore` of the
# solution is slow — task agents restore what they build. Nothing needed here.
echo "Post-merge setup: nothing to do (dotnet projects restore on build)."
