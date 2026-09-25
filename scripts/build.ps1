param([string]$Project = 'src/ShotCab.App/ShotCab.App.csproj', [string]$Configuration = 'Release', [switch]$Test)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
foreach ($taskDir in @('.cache/nuget', '.cache/http', '.cache/dotnet', '.cache/temp', 'artifacts')) { New-Item -ItemType Directory -Force $taskDir | Out-Null }
$env:NUGET_PACKAGES = Join-Path $taskRoot '.cache/nuget'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $taskRoot '.cache/http'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.cache/dotnet'
$env:TEMP = Join-Path $taskRoot '.cache/temp'
$env:TMP = $env:TEMP
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet build $Project -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Test) {
  & "$taskRoot/tests/ShotCab.Core.Tests/bin/$Configuration/net48/ShotCab.Core.Tests.exe"
  exit $LASTEXITCODE
}
