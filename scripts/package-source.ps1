param([string]$Name = 'ShotCab-source')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if ($Name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid package name.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskOutput = Join-Path $taskRoot ('artifacts/' + $Name + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
New-Item -ItemType Directory -Force -Path (Split-Path $taskOutput -Parent) | Out-Null
$taskArchive = [IO.Compression.ZipFile]::Open($taskOutput, [IO.Compression.ZipArchiveMode]::Create)
try {
    # Explicit source roots exclude local user data, caches, models and compiled output.
    $taskFiles = @(Get-ChildItem -LiteralPath $taskRoot -File | Where-Object { $_.Name -in @('.gitignore', 'Directory.Build.targets', 'README.md', 'LICENSE', 'NOTICE.md') })
    foreach ($taskDirectory in @('src', 'tests', 'scripts', 'docs', 'assets')) {
        $taskFiles += Get-ChildItem -LiteralPath (Join-Path $taskRoot $taskDirectory) -Recurse -File |
            Where-Object { $_.FullName.Substring($taskRoot.Length) -notmatch '[\\/](bin|obj|\.git|\.vs|ShotCab\.Data)[\\/]|[\\/]assets[\\/]icons[\\/]|[\\/](settings\.json|data-location\.txt)$|\.(pfx|p12|pem|key|db|sqlite|sqlite3|log)$' }
    }
    $taskUpstreamRoot = Join-Path $taskRoot 'upstream/ShareX'
    $taskFiles += Get-ChildItem -LiteralPath $taskUpstreamRoot -File |
        Where-Object { $_.Name -in @('LICENSE.txt', 'README.md', 'Directory.build.props', 'Directory.build.targets', '.editorconfig', '.gitattributes', '.gitignore') }
    foreach ($taskDirectory in @('Licenses', 'ShareX.HelpersLib', 'ShareX.ImageEffectsLib', 'ShareX.MediaLib', 'ShareX.ScreenCaptureLib')) {
        $taskFiles += Get-ChildItem -LiteralPath (Join-Path $taskUpstreamRoot $taskDirectory) -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    }
    foreach ($taskFile in $taskFiles) {
        $taskEntry = $taskFile.FullName.Substring($taskRoot.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive, $taskFile.FullName, $taskEntry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $taskArchive.Dispose() }
$taskCheck = [IO.Compression.ZipFile]::OpenRead($taskOutput)
try {
    foreach ($taskRequired in @('scripts/build.ps1', 'src/ShotCab.App/CabinetContext.cs', 'upstream/ShareX/LICENSE.txt', 'Directory.Build.targets')) {
        if (!$taskCheck.GetEntry($taskRequired)) { throw "Source package missing: $taskRequired" }
    }
    if ($taskCheck.Entries | Where-Object { $_.FullName -match '(^|/)(bin|obj|\.cache|\.git|ShotCab\.Data)/|history\.db$|data-location\.txt$|\.(pfx|p12|pem|key)$' }) { throw 'Source archive contains runtime data or a credential file.' }
    Write-Output "Source entries: $($taskCheck.Entries.Count)"
} finally { $taskCheck.Dispose() }
Write-Output "Source package: $taskOutput"
Get-FileHash -LiteralPath $taskOutput -Algorithm SHA256
