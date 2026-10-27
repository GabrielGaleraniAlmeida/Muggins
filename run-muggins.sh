#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"
dotnet run --project Muggins.Web/Muggins.Web.csproj --launch-profile Muggins.Web
