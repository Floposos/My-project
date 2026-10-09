#!/bin/bash
# Führt die Logik-Tests mit dem .NET aus, das Unity mitbringt (kein eigenes .NET nötig).
set -e
cd "$(dirname "$0")"
DOTNET="${DOTNET:-$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet 2>/dev/null | tail -1)}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
"$DOTNET" test "$@"
