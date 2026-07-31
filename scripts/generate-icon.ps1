# Генерация иконки приложения Assets/app.ico (PNG-фреймы в ICO, Vista+).
# Использование: powershell -ExecutionPolicy Bypass -File scripts/generate-icon.ps1
Add-Type -AssemblyName System.Drawing

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$assetsDir = Join-Path (Split-Path -Parent $scriptDir) 'src\DnsManager.App\Assets'
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

# --- Рисуем 256x256 master ---
$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::Transparent)

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 40
$path.AddArc($r, $r, 2 * $r, 2 * $r, 180, 90)
$path.AddArc($size - 3 * $r, $r, 2 * $r, 2 * $r, 270, 90)
$path.AddArc($size - 3 * $r, $size - 3 * $r, 2 * $r, 2 * $r, 0, 90)
$path.AddArc($r, $size - 3 * $r, 2 * $r, 2 * $r, 90, 90)
$path.CloseFigure()

$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Rectangle(0, 0, $size, $size)),
    [System.Drawing.Color]::FromArgb(0, 120, 215),
    [System.Drawing.Color]::FromArgb(0, 55, 150),
    45)
$g.FillPath($brush, $path)

$font = New-Object System.Drawing.Font('Segoe UI', 78, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$rectF = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
$g.DrawString('DNS', $font, [System.Drawing.Brushes]::White, $rectF, $sf)
$g.Dispose()

# --- Масштабируем в PNG-фреймы ---
$sizes = @(16, 32, 48, 256)
$pngFiles = @()
foreach ($s in $sizes) {
    $b = New-Object System.Drawing.Bitmap $s, $s
    $gg = [System.Drawing.Graphics]::FromImage($b)
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.DrawImage($bmp, 0, 0, $s, $s)
    $png = Join-Path $assetsDir ("tmp_icon_{0}.png" -f $s)
    $b.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $gg.Dispose(); $b.Dispose()
    $pngFiles += $png
}
$bmp.Dispose()

# --- Упаковка ICO ---
$icoPath = Join-Path $assetsDir 'app.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$pngFiles.Count)

$offset = 6 + 16 * $pngFiles.Count
$entries = @()
foreach ($p in $pngFiles) {
    $bytes = [System.IO.File]::ReadAllBytes($p)
    # Ширина/высота из IHDR (bytes 16..23, big-endian)
    $w = ($bytes[16] -shl 24) -bor ($bytes[17] -shl 16) -bor ($bytes[18] -shl 8) -bor $bytes[19]
    $h = ($bytes[20] -shl 24) -bor ($bytes[21] -shl 16) -bor ($bytes[22] -shl 8) -bor $bytes[23]
    $entries += [PSCustomObject]@{ Bytes = $bytes; W = $w; H = $h }
}

foreach ($e in $entries) {
    $bw.Write([Byte]($(if ($e.W -ge 256) { 0 } else { $e.W })))
    $bw.Write([Byte]($(if ($e.H -ge 256) { 0 } else { $e.H })))
    $bw.Write([Byte]0)          # цветов
    $bw.Write([Byte]0)          # зарезервировано
    $bw.Write([UInt16]1)        # planes
    $bw.Write([UInt16]32)       # bitcount
    $bw.Write([UInt32]$e.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $e.Bytes.Length
}
foreach ($e in $entries) { $bw.Write($e.Bytes) }
$bw.Close(); $fs.Close()

foreach ($p in $pngFiles) { Remove-Item $p -Force }
Write-Host "OK: $icoPath"
