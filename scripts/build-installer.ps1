param(
  [string]$NsisPath = '',
  [switch]$StageOnly
)

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
$taskVersion = ([xml](Get-Content 'src/ShotCab.App/ShotCab.App.csproj' -Raw)).Project.PropertyGroup.Version
if ($taskVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Unexpected app version: $taskVersion" }
$taskStageName = 'ShotCab-installer-stage-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$taskStage = Join-Path $taskRoot (Join-Path 'artifacts' $taskStageName)
if (Test-Path -LiteralPath $taskStage) { throw "Stage already exists: $taskStage" }
$taskApp = Join-Path $taskStage 'app'
$taskOcr = Join-Path $taskStage 'ocr'
$taskOcrBuild = Join-Path $taskStage 'ocr-build'
foreach ($taskDirectory in @($taskApp, $taskOcr, $taskOcrBuild, (Join-Path $taskApp 'licenses'))) {
  New-Item -ItemType Directory -Force -Path $taskDirectory | Out-Null
}

& "$PSScriptRoot/build.ps1" *> (Join-Path $taskStage 'main-build.log')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $taskStage 'main-build.log') -Tail 35; throw 'ShotCab Release build failed.' }
$taskBinary = Join-Path $taskRoot 'src/ShotCab.App/bin/Release/net48'
$taskMainFiles = @(
  'ShotCab.exe', 'ShotCab.exe.config', 'ShotCab.Core.dll',
  'ShareX.HelpersLib.dll', 'ShareX.ImageEffectsLib.dll', 'ShareX.MediaLib.dll', 'ShareX.ScreenCaptureLib.dll',
  'Microsoft.Data.Sqlite.dll', 'Newtonsoft.Json.dll', 'ImageListView.dll', 'zxing.dll', 'zxing.presentation.dll',
  'SQLitePCLRaw.batteries_v2.dll', 'SQLitePCLRaw.core.dll', 'SQLitePCLRaw.provider.dynamic_cdecl.dll',
  'e_sqlite3.dll', 'System.Buffers.dll', 'System.Memory.dll', 'System.Numerics.Vectors.dll',
  'System.Resources.Extensions.dll', 'System.Runtime.CompilerServices.Unsafe.dll'
)
foreach ($taskName in $taskMainFiles) {
  $taskSource = Join-Path $taskBinary $taskName
  if (!(Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "Required runtime file missing: $taskSource" }
  Copy-Item -LiteralPath $taskSource -Destination $taskApp
}
$taskChinese = Join-Path $taskApp 'zh-CN'
New-Item -ItemType Directory -Path $taskChinese | Out-Null
foreach ($taskName in @('ShareX.HelpersLib.resources.dll', 'ShareX.ImageEffectsLib.resources.dll', 'ShareX.MediaLib.resources.dll', 'ShareX.ScreenCaptureLib.resources.dll')) {
  $taskSource = Join-Path $taskBinary (Join-Path 'zh-CN' $taskName)
  if (!(Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "Chinese runtime resource missing: $taskSource" }
  Copy-Item -LiteralPath $taskSource -Destination $taskChinese
}
Copy-Item -LiteralPath 'packaging/README-INSTALL.txt' -Destination (Join-Path $taskApp 'README.txt')
[IO.File]::WriteAllText((Join-Path $taskApp 'installed.mode'), 'ShotCab per-user installation' + [Environment]::NewLine)

$taskLicenseSources = @{
  'ShotCab-GPL-3.0.txt' = 'LICENSE'
  'ShotCab-NOTICE.md' = 'NOTICE.md'
  'ShareX-GPL-3.0.txt' = 'upstream/ShareX/LICENSE.txt'
  'Fugue-CC-BY-3.0.txt' = 'upstream/ShareX/Licenses/Icons_license.txt'
  'ImageListView-Apache-2.0.txt' = 'upstream/ShareX/Licenses/ImageListView_license.txt'
  'ZXing-Apache-2.0.txt' = 'upstream/ShareX/Licenses/ZXing.Net_license.txt'
  'Newtonsoft-Json-MIT.txt' = 'upstream/ShareX/Licenses/Json.NET_license.txt'
  'Apache-2.0.txt' = 'packaging/licenses/Apache-2.0.txt'
  'DotNet-LICENSE.txt' = 'packaging/licenses/DotNet-LICENSE.txt'
  'Clipper2-Boost-1.0.txt' = '.cache/nuget/clipper2/2.0.0/License.txt'
  'ONNX-Runtime-MIT.txt' = '.cache/nuget/microsoft.ml.onnxruntime/1.29.0/LICENSE'
  'ONNX-Runtime-Managed-MIT.txt' = '.cache/nuget/microsoft.ml.onnxruntime.managed/1.29.0/LICENSE.txt'
  'SkiaSharp-MIT.txt' = '.cache/nuget/skiasharp/3.119.1/LICENSE.txt'
  'OCR-NOTICE.md' = 'docs/OCR-NOTICE.md'
}
foreach ($taskEntry in $taskLicenseSources.GetEnumerator()) {
  $taskSource = Join-Path $taskRoot $taskEntry.Value
  if (!(Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "License file missing: $taskSource" }
  Copy-Item -LiteralPath $taskSource -Destination (Join-Path (Join-Path $taskApp 'licenses') $taskEntry.Key)
}

$taskModelCache = 'artifacts/ShotCab.Ocr/models/v6'
& "$PSScriptRoot/build-ocr.ps1" -Destination (Join-Path 'artifacts' (Join-Path $taskStageName 'ocr-build')) -ModelCache $taskModelCache *> (Join-Path $taskStage 'ocr-build.log')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $taskStage 'ocr-build.log') -Tail 35; throw 'OCR Release build failed.' }

$taskOcrExcluded = @('createdump.exe', 'clretwrc.dll', 'clrgc.dll', 'mscordbi.dll', 'Microsoft.DiaSymReader.Native.amd64.dll')
foreach ($taskFile in Get-ChildItem -LiteralPath $taskOcrBuild -File) {
  if ($taskFile.Name -in $taskOcrExcluded -or $taskFile.Name -like 'mscordaccore*') { continue }
  if ($taskFile.Extension -notin @('.exe', '.dll', '.json')) { continue }
  Copy-Item -LiteralPath $taskFile.FullName -Destination $taskOcr
}
foreach ($taskRequired in @('ShotCab.Ocr.exe', 'ShotCab.Ocr.dll', 'ShotCab.Ocr.deps.json', 'ShotCab.Ocr.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'onnxruntime.dll', 'libSkiaSharp.dll')) {
  if (!(Test-Path -LiteralPath (Join-Path $taskOcr $taskRequired) -PathType Leaf)) { throw "Required OCR runtime file missing: $taskRequired" }
}
$taskModels = Join-Path $taskOcr 'models/v6'
New-Item -ItemType Directory -Force -Path $taskModels | Out-Null
$taskExpectedModels = @{
  'PP-OCRv6_det_small.onnx' = '090f04abcd9d9a7498bc4ebf677e4cb9bdce1fe4197ddb7e529f1ef44e1ff94f'
  'PP-OCRv6_rec_small.onnx' = '6f327246b50388f3c176ae304bd95767ea6dc0c9ae92153ef8cbe210b3c14884'
  'ppocrv6_dict.txt' = 'b5f2bfe2bdd9448429e3e82b51c789775d9b42f2403d082b00662eb77e401c5d'
}
foreach ($taskEntry in $taskExpectedModels.GetEnumerator()) {
  $taskSource = Join-Path (Join-Path $taskOcrBuild 'models/v6') $taskEntry.Key
  if (!(Test-Path -LiteralPath $taskSource -PathType Leaf)) { throw "OCR model missing: $($taskEntry.Key)" }
  if ((Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash -ne $taskEntry.Value) { throw "OCR model checksum mismatch: $($taskEntry.Key)" }
  Copy-Item -LiteralPath $taskSource -Destination $taskModels
}
$taskClassifierName = 'ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx'
$taskClassifierSha = '54379ae5174d026780215fc748a7f31910dee36818e63d49e17dc598ecc82df7'
$taskClassifierSource = Join-Path $taskOcrBuild (Join-Path 'models/v5' $taskClassifierName)
if (!(Test-Path -LiteralPath $taskClassifierSource -PathType Leaf)) { throw "OCR classifier missing: $taskClassifierName" }
if ((Get-FileHash -LiteralPath $taskClassifierSource -Algorithm SHA256).Hash -ne $taskClassifierSha) { throw "OCR classifier checksum mismatch: $taskClassifierName" }
$taskClassifierDirectory = Join-Path $taskOcr 'models/v5'
New-Item -ItemType Directory -Force -Path $taskClassifierDirectory | Out-Null
Copy-Item -LiteralPath $taskClassifierSource -Destination $taskClassifierDirectory

$taskPayloadFiles = @(Get-ChildItem -LiteralPath $taskApp, $taskOcr -Recurse -File)
foreach ($taskFile in $taskPayloadFiles) {
  $taskRelative = $taskFile.FullName.Substring($taskStage.Length + 1).Replace('\', '/')
  if ($taskRelative -match '(?i)(^|/)(ShotCab\.Data|\.cache|artifacts|tests|obj|bin)(/|$)|(^|/)(settings\.json|history\.db|data-location\.txt|startup\.log)$|\.(pdb|lib|pfx|p12|pem|key|zip)$') {
    throw "Developer or user file in installer payload: $taskRelative"
  }
}
foreach ($taskFile in Get-ChildItem -LiteralPath $taskApp, $taskOcr -Recurse -File | Where-Object { $_.Extension -in @('.md', '.txt', '.json', '.config') }) {
  if (Select-String -LiteralPath $taskFile.FullName -Pattern 'F:\\AI coding\\SC', 'C:\\Users\\ASUS' -Quiet) { throw "Developer path in installer payload: $($taskFile.FullName)" }
}

function Write-UninstallList([string]$taskDirectory, [string]$taskPrefix, [string]$taskOutputFile) {
  $taskLines = New-Object 'System.Collections.Generic.List[string]'
  foreach ($taskFile in Get-ChildItem -LiteralPath $taskDirectory -Recurse -File) {
    $taskRelative = $taskFile.FullName.Substring($taskDirectory.Length + 1)
    $taskLines.Add('Delete "$INSTDIR\' + $taskPrefix + $taskRelative + '"')
  }
  foreach ($taskSubdirectory in (Get-ChildItem -LiteralPath $taskDirectory -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending)) {
    $taskRelative = $taskSubdirectory.FullName.Substring($taskDirectory.Length + 1)
    $taskLines.Add('RMDir "$INSTDIR\' + $taskPrefix + $taskRelative + '"')
  }
  if ($taskPrefix) { $taskLines.Add('RMDir "$INSTDIR\' + $taskPrefix.TrimEnd('\') + '"') }
  [IO.File]::WriteAllLines($taskOutputFile, $taskLines, (New-Object System.Text.UTF8Encoding($false)))
}
Write-UninstallList $taskApp '' (Join-Path $taskStage 'uninstall-app.nsh')
Write-UninstallList $taskOcr 'ocr\' (Join-Path $taskStage 'uninstall-ocr.nsh')

$taskManifest = New-Object 'System.Collections.Generic.List[string]'
$taskManifest.Add("component`tpath`tsha256`tbytes")
foreach ($taskFile in $taskPayloadFiles | Sort-Object FullName) {
  $taskRelative = $taskFile.FullName.Substring($taskStage.Length + 1).Replace('\', '/')
  $taskManifest.Add((@($taskRelative.Split('/')[0], $taskRelative, (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $taskFile.Length) -join "`t"))
}
[IO.File]::WriteAllLines((Join-Path $taskStage 'payload-manifest.tsv'), $taskManifest, (New-Object System.Text.UTF8Encoding($false)))
$taskPayloadBytes = ($taskPayloadFiles | Measure-Object -Property Length -Sum).Sum
Write-Output "Verified payload: $($taskPayloadFiles.Count) files; $taskPayloadBytes bytes; OCR is optional in setup."
Write-Output "Stage: $taskStage"
if ($StageOnly) { return }

if (!$NsisPath) { $taskCommand = Get-Command makensis.exe -ErrorAction SilentlyContinue; if ($taskCommand) { $NsisPath = $taskCommand.Source } }
if (!(Test-Path -LiteralPath $NsisPath -PathType Leaf)) { throw 'NSIS 3.12 makensis.exe is required; pass -NsisPath on F:.' }
$taskOutput = Join-Path $taskRoot (Join-Path 'artifacts' "ShotCab-$taskVersion-win-x64-setup.exe")
& $NsisPath '/INPUTCHARSET' 'UTF8' "/DSTAGE=$taskStage" "/DOUTFILE=$taskOutput" "/DROOT=$taskRoot" "/DAPP_VERSION=$taskVersion" (Join-Path $taskRoot 'packaging/ShotCab.nsi') *> (Join-Path $taskStage 'nsis-build.log')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $taskStage 'nsis-build.log') -Tail 55; throw 'NSIS installer compilation failed.' }
if (!(Test-Path -LiteralPath $taskOutput -PathType Leaf)) { throw 'NSIS completed without creating the installer.' }
Write-Output "Installer: $taskOutput"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $taskOutput -Algorithm SHA256).Hash)"
