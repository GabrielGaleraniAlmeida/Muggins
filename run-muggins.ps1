$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet run --project (Join-Path $PSScriptRoot "Muggins.Web/Muggins.Web.csproj") --launch-profile Muggins.Web
exit $LASTEXITCODE
