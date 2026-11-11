# ==============================================================================
# NEXUS-3D - Brand Icon & Asset Generator
# Produces high-resolution transparent PNG and multi-size Windows ICO
# ==============================================================================

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Drawing

$rootDir = (Get-Item $PSScriptRoot).Parent.FullName
$brandingDir = Join-Path $rootDir "assets\branding"
if (-not (Test-Path $brandingDir)) {
    New-Item -ItemType Directory -Path $brandingDir -Force | Out-Null
}

$pngOut = Join-Path $brandingDir "nexus3d-icon.png"
$icoOut = Join-Path $brandingDir "app.ico"
$appResDir = Join-Path $rootDir "src\NEXUS3D.App\Resources"
if (-not (Test-Path $appResDir)) {
    New-Item -ItemType Directory -Path $appResDir -Force | Out-Null
}

function Create-NexusDrawingVisual([int]$size) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $scale = $size / 512.0

    # Transformation to scale
    $transform = New-Object System.Windows.Media.ScaleTransform($scale, $scale)
    $dc.PushTransform($transform)

    # Brushes
    $primaryColor = [System.Windows.Media.Color]::FromRgb(0x67, 0x50, 0xF5) # #6750F5
    $primaryLight = [System.Windows.Media.Color]::FromRgb(0x84, 0x71, 0xFA)
    $secondaryColor = [System.Windows.Media.Color]::FromRgb(0x18, 0xB6, 0xA4) # #18B6A4
    $secondaryLight = [System.Windows.Media.Color]::FromRgb(0x22, 0xD3, 0xBE)
    $accentColor = [System.Windows.Media.Color]::FromRgb(0x49, 0xA6, 0xFF) # #49A6FF
    $accentLight = [System.Windows.Media.Color]::FromRgb(0x6B, 0xB5, 0xFF)

    # Top face gradient
    $topBrush = New-Object System.Windows.Media.LinearGradientBrush(
        $primaryLight, $primaryColor,
        (New-Object System.Windows.Point(0, 0)),
        (New-Object System.Windows.Point(1, 1))
    )

    # Left face gradient
    $leftBrush = New-Object System.Windows.Media.LinearGradientBrush(
        $secondaryLight, $secondaryColor,
        (New-Object System.Windows.Point(0, 0)),
        (New-Object System.Windows.Point(1, 1))
    )

    # Right face gradient
    $rightBrush = New-Object System.Windows.Media.LinearGradientBrush(
        $accentLight, $accentColor,
        (New-Object System.Windows.Point(0, 0)),
        (New-Object System.Windows.Point(1, 1))
    )

    # Draw Top Face (Polygonal Mesh)
    $topGeometry = New-Object System.Windows.Media.StreamGeometry
    $ctx = $topGeometry.Open()
    $ctx.BeginFigure((New-Object System.Windows.Point(256, 128)), $true, $true)
    $ctx.LineTo((New-Object System.Windows.Point(384, 202)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(256, 276)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(128, 202)), $true, $false)
    $ctx.Close()
    $topPen = New-Object System.Windows.Media.Pen(
        (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x9B, 0x8B, 0xFA))), 2.5
    )
    $dc.DrawGeometry($topBrush, $topPen, $topGeometry)

    # Top Mesh Lines
    $meshPen = New-Object System.Windows.Media.Pen(
        (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromArgb(180, 0xC2, 0xB7, 0xFF))), 1.8
    )
    $dc.DrawLine($meshPen, (New-Object System.Windows.Point(192, 165)), (New-Object System.Windows.Point(320, 239)))
    $dc.DrawLine($meshPen, (New-Object System.Windows.Point(256, 128)), (New-Object System.Windows.Point(256, 276)))
    $dc.DrawLine($meshPen, (New-Object System.Windows.Point(192, 239)), (New-Object System.Windows.Point(320, 165)))
    $dc.DrawLine($meshPen, (New-Object System.Windows.Point(128, 202)), (New-Object System.Windows.Point(384, 202)))

    # Draw Left Face (Voxel Cube)
    $leftGeometry = New-Object System.Windows.Media.StreamGeometry
    $ctx = $leftGeometry.Open()
    $ctx.BeginFigure((New-Object System.Windows.Point(128, 202)), $true, $true)
    $ctx.LineTo((New-Object System.Windows.Point(256, 276)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(256, 384)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(128, 310)), $true, $false)
    $ctx.Close()
    $leftPen = New-Object System.Windows.Media.Pen(
        (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x3B, 0xE1, 0xCE))), 2.5
    )
    $dc.DrawGeometry($leftBrush, $leftPen, $leftGeometry)

    # Voxel Blocks on Left Face
    $voxelSubPen = New-Object System.Windows.Media.Pen(
        (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x4E, 0xF1, 0xDF))), 1.5
    )
    $voxelSubBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x20, 0xC4, 0xB1))
    $voxelGeo = New-Object System.Windows.Media.StreamGeometry
    $ctx = $voxelGeo.Open()
    $ctx.BeginFigure((New-Object System.Windows.Point(160, 256)), $true, $true)
    $ctx.LineTo((New-Object System.Windows.Point(192, 274)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(192, 311)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(160, 293)), $true, $false)
    $ctx.Close()
    $dc.DrawGeometry($voxelSubBrush, $voxelSubPen, $voxelGeo)

    # Draw Right Face (Geometry)
    $rightGeometry = New-Object System.Windows.Media.StreamGeometry
    $ctx = $rightGeometry.Open()
    $ctx.BeginFigure((New-Object System.Windows.Point(256, 276)), $true, $true)
    $ctx.LineTo((New-Object System.Windows.Point(384, 202)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(384, 310)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(256, 384)), $true, $false)
    $ctx.Close()
    $rightPen = New-Object System.Windows.Media.Pen(
        (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x75, 0xBF, 0xFF))), 2.5
    )
    $dc.DrawGeometry($rightBrush, $rightPen, $rightGeometry)

    # Geometry Bone Connections
    $geoPen = New-Object System.Windows.Media.Pen(
        (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0xA8, 0xD5, 0xFF))), 2.0
    )
    $dc.DrawLine($geoPen, (New-Object System.Windows.Point(288, 294)), (New-Object System.Windows.Point(352, 257)))
    $dc.DrawLine($geoPen, (New-Object System.Windows.Point(288, 365)), (New-Object System.Windows.Point(352, 328)))
    $dc.DrawLine($geoPen, (New-Object System.Windows.Point(320, 239)), (New-Object System.Windows.Point(320, 347)))

    # Joint Node on Right Face
    $jointBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Colors]::White)
    $jointPen = New-Object System.Windows.Media.Pen(
        (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x24, 0x78, 0xCC))), 1.5
    )
    $dc.DrawEllipse($jointBrush, $jointPen, (New-Object System.Windows.Point(320, 293)), 5, 5)

    # Orbital Connections
    $orbitPen = New-Object System.Windows.Media.Pen(
        (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromArgb(200, 0x6B, 0xB5, 0xFF))), 2.0
    )
    $dc.DrawEllipse((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x6B, 0xB5, 0xFF))), $null, (New-Object System.Windows.Point(256, 62)), 7, 7)
    $dc.DrawEllipse((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x84, 0x71, 0xFA))), $null, (New-Object System.Windows.Point(438, 168)), 6, 6)
    $dc.DrawEllipse((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x22, 0xD3, 0xBE))), $null, (New-Object System.Windows.Point(438, 344)), 6.5, 6.5)
    $dc.DrawEllipse((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x18, 0xB6, 0xA4))), $null, (New-Object System.Windows.Point(256, 450)), 7, 7)
    $dc.DrawEllipse((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x49, 0xA6, 0xFF))), $null, (New-Object System.Windows.Point(74, 344)), 6.5, 6.5)
    $dc.DrawEllipse((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x67, 0x50, 0xF5))), $null, (New-Object System.Windows.Point(74, 168)), 6, 6)

    # Central Core Node
    $dc.DrawEllipse((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Colors]::White)), $null, (New-Object System.Windows.Point(256, 276)), 9, 9)
    $dc.DrawEllipse((New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x67, 0x50, 0xF5))), $null, (New-Object System.Windows.Point(256, 276)), 5, 5)

    $dc.Pop()
    $dc.Close()
    return $visual
}

function Render-Bitmap([int]$size) {
    $visual = Create-NexusDrawingVisual $size
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($visual)
    return $rtb
}

# 1. Save 512x512 Master PNG
Write-Host "Rendering 512x512 Master Icon PNG..." -ForegroundColor Cyan
$masterRtb = Render-Bitmap 512
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($masterRtb))
$fs = [System.IO.File]::Create($pngOut)
$encoder.Save($fs)
$fs.Close()
Write-Host "Saved: $pngOut" -ForegroundColor Green

# 2. Generate Multi-size Windows ICO (16, 32, 48, 64, 128, 256)
Write-Host "Generating Multi-resolution Windows ICO..." -ForegroundColor Cyan
$sizes = @(16, 32, 48, 64, 128, 256)
$pngStreams = @()

foreach ($sz in $sizes) {
    $rtb = Render-Bitmap $sz
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $ms = New-Object System.IO.MemoryStream
    $enc.Save($ms)
    $pngStreams += @{ Size = $sz; Bytes = $ms.ToArray() }
    $ms.Close()
}

$icoStream = [System.IO.File]::Create($icoOut)
$bw = New-Object System.IO.BinaryWriter($icoStream)

# ICONDIR header: Reserved (0), Type (1 = Icon), Count
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$pngStreams.Count)

$offset = 6 + ($pngStreams.Count * 16)
foreach ($item in $pngStreams) {
    $bSize = if ($item.Size -ge 256) { 0 } else { [byte]$item.Size }
    $bw.Write([byte]$bSize)         # Width
    $bw.Write([byte]$bSize)         # Height
    $bw.Write([byte]0)              # Color count
    $bw.Write([byte]0)              # Reserved
    $bw.Write([uint16]1)            # Color planes
    $bw.Write([uint16]32)           # Bits per pixel
    $bw.Write([uint32]$item.Bytes.Length) # Image data size
    $bw.Write([uint32]$offset)      # Offset
    $offset += $item.Bytes.Length
}

foreach ($item in $pngStreams) {
    $bw.Write($item.Bytes)
}

$bw.Flush()
$icoStream.Close()
Write-Host "Saved: $icoOut" -ForegroundColor Green

# Copy to App Resources
$appIcoPath = Join-Path $appResDir "app.ico"
Copy-Item $icoOut -Destination $appIcoPath -Force
Write-Host "Copied icon to: $appIcoPath" -ForegroundColor Green

