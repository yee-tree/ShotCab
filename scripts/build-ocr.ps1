param([string]$Destination = 'artifacts/ShotCab.Ocr', [string]$ModelCache = '')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Set-Location $taskRoot
& "$PSScriptRoot/build.ps1" -Project src/ShotCab.Ocr/ShotCab.Ocr.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskDestination = [IO.Path]::GetFullPath((Join-Path $taskRoot $Destination))
if ($taskDestination.StartsWith('C:', [StringComparison]::OrdinalIgnoreCase)) { throw 'C盘下载需要用户确认，请选择其他盘。' }
dotnet publish src/ShotCab.Ocr/ShotCab.Ocr.csproj -c Release -r win-x64 --self-contained true -o $taskDestination --nologo -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskModels = Join-Path $taskDestination 'models/v6'
New-Item -ItemType Directory -Force $taskModels | Out-Null
$taskDownloads = @(
  @{Name='PP-OCRv6_det_small.onnx'; Url='https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv6/det/PP-OCRv6_det_small.onnx'; Sha='090f04abcd9d9a7498bc4ebf677e4cb9bdce1fe4197ddb7e529f1ef44e1ff94f'},
  @{Name='PP-OCRv6_rec_small.onnx'; Url='https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/onnx/PP-OCRv6/rec/PP-OCRv6_rec_small.onnx'; Sha='6f327246b50388f3c176ae304bd95767ea6dc0c9ae92153ef8cbe210b3c14884'},
  @{Name='ppocrv6_dict.txt'; Url='https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.2/paddle/PP-OCRv6/rec/PP-OCRv6_rec_small/ppocrv6_dict.txt'; Sha='b5f2bfe2bdd9448429e3e82b51c789775d9b42f2403d082b00662eb77e401c5d'}
)
foreach ($taskDownload in $taskDownloads) {
  $taskFile = Join-Path $taskModels $taskDownload.Name
  if (!(Test-Path $taskFile) -and $ModelCache) {
    $taskCached = Join-Path ([IO.Path]::GetFullPath((Join-Path $taskRoot $ModelCache))) $taskDownload.Name
    if (Test-Path -LiteralPath $taskCached) {
      if ((Get-FileHash -LiteralPath $taskCached -Algorithm SHA256).Hash -ne $taskDownload.Sha) { throw "模型缓存校验失败：$($taskDownload.Name)" }
      Copy-Item -LiteralPath $taskCached -Destination $taskFile
    }
  }
  if (!(Test-Path $taskFile)) { Invoke-WebRequest -Uri $taskDownload.Url -OutFile ($taskFile + '.download'); Move-Item -LiteralPath ($taskFile + '.download') -Destination $taskFile }
  if ($taskDownload.Sha -and (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash -ne $taskDownload.Sha) { throw "模型校验失败：$($taskDownload.Name)" }
}
Copy-Item docs/OCR-NOTICE.md -Destination $taskDestination
Write-Host "离线OCR组件已准备：$taskDestination"
