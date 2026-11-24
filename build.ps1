param(
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    throw "C# compiler not found at $csc"
}

$assetsDir = Join-Path $root "assets"
if (-not (Test-Path $assetsDir)) { New-Item -ItemType Directory -Path $assetsDir | Out-Null }
$icoPath = Join-Path $assetsDir "KeyFixer.ico"

# Generate crisp 32x32 .ico (PNG-in-ICO format supported by Windows Vista+)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(32, 32)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::Transparent)

$rect = New-Object System.Drawing.Rectangle(1, 1, 29, 29)
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$d = 14
$path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
$path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
$path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
$path.CloseFigure()

$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(25, 118, 210), [System.Drawing.Color]::FromArgb(13, 71, 161), 90.0)
$border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 255, 255, 255), 1.2)
$g.FillPath($bg, $path)
$g.DrawPath($border, $path)

$fontEn = New-Object System.Drawing.Font("Segoe UI", 10.5, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Point)
$fontAr = New-Object System.Drawing.Font("Segoe UI", 11.0, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Point)
$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$accent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(144, 238, 255))
$g.DrawString("A", $fontEn, $white, 2.5, 2.5)
$g.DrawString([char]0x0639, $fontAr, $accent, 14.0, 10.5)

$msPng = New-Object System.IO.MemoryStream
$bmp.Save($msPng, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $msPng.ToArray()

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
# ICONDIR (6 bytes)
$bw.Write([UInt16]0)   # reserved
$bw.Write([UInt16]1)   # type = ICO
$bw.Write([UInt16]1)   # count = 1
# ICONDIRENTRY (16 bytes)
$bw.Write([Byte]32)    # width
$bw.Write([Byte]32)    # height
$bw.Write([Byte]0)     # color palette
$bw.Write([Byte]0)     # reserved
$bw.Write([UInt16]1)   # planes
$bw.Write([UInt16]32)  # bpp
$bw.Write([UInt32]$pngBytes.Length)
$bw.Write([UInt32]22)  # offset = 6 + 16
$bw.Write($pngBytes)
$bw.Flush()
$bw.Close()
$fs.Dispose()
$msPng.Dispose()
$accent.Dispose(); $white.Dispose(); $fontAr.Dispose(); $fontEn.Dispose()
$border.Dispose(); $bg.Dispose(); $path.Dispose(); $g.Dispose(); $bmp.Dispose()

$outExe = Join-Path $root "KeyFixer.exe"
$sources = @(
    (Join-Path $root "src\AssemblyInfo.cs"),
    (Join-Path $root "src\LayoutConverter.cs"),
    (Join-Path $root "src\AppSettings.cs"),
    (Join-Path $root "src\HotkeyManager.cs"),
    (Join-Path $root "src\TextReplacer.cs"),
    (Join-Path $root "src\TrayIconFactory.cs"),
    (Join-Path $root "src\SettingsForm.cs"),
    (Join-Path $root "src\Program.cs")
)

Write-Host "Building KeyFixer.exe..."
& $csc /nologo /utf8output /codepage:65001 /target:winexe /optimize+ /warnaserror `
    "/win32icon:$icoPath" `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Web.Extensions.dll `
    "/out:$outExe" `
    @sources

if ($LASTEXITCODE -ne 0) {
    throw "Compilation of KeyFixer.exe failed with exit code $LASTEXITCODE"
}

$fi = Get-Item $outExe
Write-Host ("Built successfully: {0} ({1:N0} bytes / {2:N1} KB)" -f $fi.FullName, $fi.Length, ($fi.Length / 1KB))

if ($RunTests) {
    Write-Host "`nRunning ConverterTests..."
    $convTestExe = Join-Path $root "tests\ConverterTests.exe"
    & $csc /nologo /utf8output /codepage:65001 /warnaserror /target:exe `
        "/out:$convTestExe" `
        (Join-Path $root "src\LayoutConverter.cs") `
        (Join-Path $root "tests\ConverterTests.cs")
    if ($LASTEXITCODE -ne 0) { throw "ConverterTests compile failed" }
    & $convTestExe
    $convExit = $LASTEXITCODE
    Remove-Item $convTestExe -Force -ErrorAction SilentlyContinue
    if ($convExit -ne 0) { throw "ConverterTests failed" }

    Write-Host "`nRunning AppIntegrationTests..."
    $appTestExe = Join-Path $root "tests\AppIntegrationTests.exe"
    & $csc /nologo /utf8output /codepage:65001 /warnaserror /target:exe `
        /r:System.dll `
        /r:System.Core.dll `
        /r:System.Drawing.dll `
        /r:System.Windows.Forms.dll `
        /r:System.Web.Extensions.dll `
        "/out:$appTestExe" `
        (Join-Path $root "src\LayoutConverter.cs") `
        (Join-Path $root "src\AppSettings.cs") `
        (Join-Path $root "src\HotkeyManager.cs") `
        (Join-Path $root "src\TextReplacer.cs") `
        (Join-Path $root "src\TrayIconFactory.cs") `
        (Join-Path $root "src\SettingsForm.cs") `
        (Join-Path $root "tests\AppIntegrationTests.cs")
    if ($LASTEXITCODE -ne 0) { throw "AppIntegrationTests compile failed" }
    & $appTestExe
    $appExit = $LASTEXITCODE
    Remove-Item $appTestExe -Force -ErrorAction SilentlyContinue
    if ($appExit -ne 0) { throw "AppIntegrationTests failed" }
}
