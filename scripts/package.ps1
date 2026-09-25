param([string]$Name = 'ShotCab-preview-win-x64')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$binaryRoot = Join-Path $taskRoot 'src/ShotCab.App/bin/Release/net48'
if (!(Test-Path -LiteralPath (Join-Path $binaryRoot 'ShotCab.exe'))) { throw 'Build ShotCab first.' }
if ($Name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid package name.' }
$stage = Join-Path $taskRoot ('artifacts/' + $Name + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $stage | Out-Null
Get-ChildItem -LiteralPath $binaryRoot -File | Where-Object { $_.Extension -in @('.exe', '.dll', '.config') } | Copy-Item -Destination $stage
Get-ChildItem -LiteralPath $binaryRoot -Directory | Where-Object { $_.Name -eq 'runtimes' -or $_.Name -match '^[a-z]{2}(-[A-Z]{2})?$' } | Copy-Item -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $taskRoot 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $taskRoot 'NOTICE.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $taskRoot 'upstream/ShareX/LICENSE.txt') -Destination $stage
Copy-Item -LiteralPath (Join-Path $taskRoot 'upstream/ShareX/Licenses') -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs') -Destination $stage -Recurse
$zip = $stage + '.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$size = (Get-Item -LiteralPath $zip).Length
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    if ($archive.Entries | Where-Object { $_.FullName -match '(?i)(ShotCab\.Data|startup\.log|data-location\.txt|history\.db|ocr-jobs)' }) { throw 'Package contains runtime data.' }
} finally { $archive.Dispose() }
if ($size -gt 100MB) { throw "Package exceeds 100 MB: $size bytes" }
Write-Output "Package: $zip"
Write-Output "Bytes: $size"
Write-Output 'Preview only; public distribution also requires the complete corresponding source and dependency notices.'
