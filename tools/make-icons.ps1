# Renders the app icon in every size into CheckYourNouns/wwwroot/icons:
# two overlapping letter tiles on the dark app background - a gold "Å" tilted behind a violet-framed "É" -
# for nouns in several alphabets. The favicons, too small for two tiles, show only the front one.
#   powershell -File tools\make-icons.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot "..\CheckYourNouns\wwwroot\icons"
New-Item -ItemType Directory -Force $out | Out-Null

$violet = [System.Drawing.Color]::FromArgb(0x8B, 0x6C, 0xFF)
$gold = [System.Drawing.Color]::FromArgb(0xF5, 0xC4, 0x51)

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $path
}

# Draws one letter tile centred on ($cx, $cy), turned by $angle degrees.
function Draw-Tile($g, [float]$cx, [float]$cy, [float]$w, [float]$angle, $frame, $fill, $textColor, [string]$letter) {
    $h = $w * 1.12
    $state = $g.Save()
    $g.TranslateTransform($cx, $cy)
    $g.RotateTransform($angle)
    $path = New-RoundedRect (-$w / 2) (-$h / 2) $w $h ($w * 0.18)
    $g.FillPath((New-Object System.Drawing.SolidBrush $fill), $path)
    $g.DrawPath((New-Object System.Drawing.Pen $frame, ([Math]::Max(1.5, $w * 0.07))), $path)

    $font = New-Object System.Drawing.Font "Georgia", ([float]($h * 0.6)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = "Center"
    $format.LineAlignment = "Center"
    $rect = New-Object System.Drawing.RectangleF (-$w / 2), (-$h / 2 + $h * 0.06), $w, $h
    $g.DrawString($letter, $font, (New-Object System.Drawing.SolidBrush $textColor), $rect, $format)
    $g.Restore($state)
}

# $scale is the artwork's width as a share of the icon; maskable icons keep it inside the safe zone.
# $rounded gives the background its own rounded corners (favicons); iOS and maskable icons are rounded by the OS.
# $single draws only the front tile, for favicons.
function New-Icon([int]$size, [double]$scale = 0.74, [bool]$rounded = $false, [bool]$single = $false) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = "AntiAlias"
    $g.TextRenderingHint = "AntiAliasGridFit"
    $g.InterpolationMode = "HighQualityBicubic"
    $g.Clear([System.Drawing.Color]::Transparent)

    $rect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
        ([System.Drawing.Color]::FromArgb(0x2C, 0x25, 0x4A)), ([System.Drawing.Color]::FromArgb(0x11, 0x10, 0x1C)), 90
    if ($rounded) {
        $g.FillPath($bg, (New-RoundedRect 0 0 $size $size ($size * 0.2)))
    } else {
        $g.FillRectangle($bg, $rect)
    }

    $tileFill = [System.Drawing.Color]::FromArgb(0x24, 0x23, 0x38)
    # [char]0xC5 is "Å" and [char]0xC9 is "É", written as codes so Windows PowerShell can't misread the file encoding.
    if ($single) {
        Draw-Tile $g ($size / 2) ($size / 2) ($size * $scale * 0.86) 0 $violet $tileFill ([System.Drawing.Color]::White) ([string][char]0xC9)
    } else {
        $w = $size * $scale * 0.62
        $c = $size / 2
        $offset = $size * $scale * 0.17
        Draw-Tile $g ($c - $offset) ($c - $offset * 0.55) $w -10 $gold ([System.Drawing.Color]::FromArgb(0x1E, 0x1D, 0x2F)) $gold ([string][char]0xC5)
        Draw-Tile $g ($c + $offset) ($c + $offset * 0.55) $w 6 $violet $tileFill ([System.Drawing.Color]::White) ([string][char]0xC9)
    }

    $g.Dispose()
    $bmp
}

function Save-Png($bmp, [string]$name) {
    $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# iOS home screen (opaque, square: iOS rounds the corners itself).
Save-Png (New-Icon 180) "apple-touch-icon.png"
Save-Png (New-Icon 167) "apple-touch-icon-167.png"
Save-Png (New-Icon 152) "apple-touch-icon-152.png"

# Web app manifest (Windows/Android install) and a big master copy.
Save-Png (New-Icon 1024) "icon-1024.png"
Save-Png (New-Icon 512 0.74 $true) "icon-512.png"
Save-Png (New-Icon 192 0.74 $true) "icon-192.png"
Save-Png (New-Icon 512 0.58) "icon-512-maskable.png"

# Browser tab: one bigger tile so it reads at 16 px.
Save-Png (New-Icon 32 0.9 $true $true) "favicon-32.png"

# favicon.ico with 16, 32 and 48 px PNG images, for Windows and older browsers.
$images = foreach ($s in 16, 32, 48) {
    $bmp = New-Icon $s 0.9 $true $true
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Bytes = $ms.ToArray() }
}
$ico = New-Object System.IO.MemoryStream
$wr = New-Object System.IO.BinaryWriter $ico
$wr.Write([uint16]0); $wr.Write([uint16]1); $wr.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $wr.Write([byte]$img.Size); $wr.Write([byte]$img.Size); $wr.Write([byte]0); $wr.Write([byte]0)
    $wr.Write([uint16]1); $wr.Write([uint16]32)
    $wr.Write([uint32]$img.Bytes.Length); $wr.Write([uint32]$offset)
    $offset += $img.Bytes.Length
}
foreach ($img in $images) { $wr.Write($img.Bytes) }
$wr.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $out "..\favicon.ico"), $ico.ToArray())

Write-Host "Icons written to $out"
