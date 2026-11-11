# Renders tools\banner.html to images\banner.png (1920x1200) with headless Edge.
#   powershell -File tools\make-banner.ps1
$edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
$html = (Resolve-Path (Join-Path $PSScriptRoot "banner.html")).Path
$outDir = Join-Path $PSScriptRoot "..\images"
New-Item -ItemType Directory -Force $outDir | Out-Null
$png = Join-Path (Resolve-Path $outDir).Path "banner.png"

# The virtual time budget gives the web fonts time to load before the shot.
& $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
    --window-size=1920,1200 --virtual-time-budget=8000 --screenshot="$png" "file:///$($html -replace '\\', '/')" | Out-Null
Write-Host "Banner written to $png"
