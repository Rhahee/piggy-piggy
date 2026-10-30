param(
    [string]$Source = (Join-Path $PSScriptRoot '..\Assets\app.png'),
    [string]$Destination = (Join-Path $PSScriptRoot '..\Assets\app.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::FromFile([IO.Path]::GetFullPath($Source))
try {
    if ($bitmap.Width -ne $bitmap.Height) { throw 'Application icon must be square.' }
    if ($bitmap.GetPixel(0,0).A -ne 0) { throw 'Application icon must have transparent corners.' }
    $frames = foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
        $scaled = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($scaled)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($bitmap,[Drawing.Rectangle]::new(0,0,$size,$size),0,0,$bitmap.Width,$bitmap.Height,[Drawing.GraphicsUnit]::Pixel)
            $scaled.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
            [pscustomobject]@{ Size=$size; Bytes=$stream.ToArray() }
        } finally { $stream.Dispose(); $graphics.Dispose(); $scaled.Dispose() }
    }
    $output = [IO.File]::Create([IO.Path]::GetFullPath($Destination))
    $writer = [IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    } finally { $writer.Dispose() }
    [pscustomobject]@{ Output=[IO.Path]::GetFullPath($Destination); Sizes=@($frames.Size); SourceWidth=$bitmap.Width; SourceHeight=$bitmap.Height }
} finally { $bitmap.Dispose() }
