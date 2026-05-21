param(
    [string]$OutputPath = "src/DoipSimulator.Host/assets/doip-simulator.ico"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outputFullPath)
if (-not [System.IO.Directory]::Exists($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}

Add-Type -AssemblyName System.Drawing

$size = 256
$bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

try {
    $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, 0, $size, $size),
        [System.Drawing.Color]::FromArgb(255, 14, 27, 38),
        [System.Drawing.Color]::FromArgb(255, 30, 80, 108),
        45)
    $graphics.FillRectangle($background, 0, 0, $size, $size)

    $panelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(230, 9, 15, 22))
    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 89, 169, 255), 8)
    $greenPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 53, 208, 127), 10)
    $amberPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 240, 184, 79), 8)
    $whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 232, 241, 248))

    $rect = [System.Drawing.Rectangle]::new(26, 30, 204, 196)
    $radius = 34
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($rect.Left, $rect.Top, $radius, $radius, 180, 90)
    $path.AddArc($rect.Right - $radius, $rect.Top, $radius, $radius, 270, 90)
    $path.AddArc($rect.Right - $radius, $rect.Bottom - $radius, $radius, $radius, 0, 90)
    $path.AddArc($rect.Left, $rect.Bottom - $radius, $radius, $radius, 90, 90)
    $path.CloseFigure()
    $graphics.FillPath($panelBrush, $path)
    $graphics.DrawPath($borderPen, $path)

    $graphics.DrawLine($greenPen, 58, 150, 92, 116)
    $graphics.DrawLine($greenPen, 92, 116, 126, 142)
    $graphics.DrawLine($greenPen, 126, 142, 166, 82)
    $graphics.DrawLine($greenPen, 166, 82, 202, 118)

    $graphics.DrawLine($amberPen, 58, 174, 202, 174)
    $graphics.DrawEllipse($amberPen, 54, 58, 148, 148)

    $font = [System.Drawing.Font]::new("Segoe UI", 48, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $graphics.DrawString("DS", $font, $whiteBrush, [System.Drawing.RectangleF]::new(0, 170, $size, 68), $format)

    $pngStream = [System.IO.MemoryStream]::new()
    $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $pngStream.ToArray()

    $fileStream = [System.IO.File]::Create($outputFullPath)
    $writer = [System.IO.BinaryWriter]::new($fileStream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]1)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$pngBytes.Length)
        $writer.Write([UInt32]22)
        $writer.Write($pngBytes)
    }
    finally {
        $writer.Dispose()
        $fileStream.Dispose()
        $pngStream.Dispose()
    }
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}
