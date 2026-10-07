$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskAssets = Join-Path $taskRoot 'Bandwidth\Assets'
New-Item -ItemType Directory -Path $taskAssets -Force | Out-Null

# Draw a scalable vector composition, then package the PNG sizes in one Windows ICO.
$taskMaster = [Drawing.Bitmap]::new(512, 512)
$taskGraphics = [Drawing.Graphics]::FromImage($taskMaster)
$taskGraphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$taskGraphics.ScaleTransform(8, 8)
$taskShape = [Drawing.Drawing2D.GraphicsPath]::new()
$taskShape.AddArc(2, 2, 22, 22, 180, 90)
$taskShape.AddArc(40, 2, 22, 22, 270, 90)
$taskShape.AddArc(40, 40, 22, 22, 0, 90)
$taskShape.AddArc(2, 40, 22, 22, 90, 90)
$taskShape.CloseFigure()
$taskGradient = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(8, 2), [Drawing.Point]::new(50, 64), [Drawing.ColorTranslator]::FromHtml('#2B4667'), [Drawing.ColorTranslator]::FromHtml('#0B1423'))
$taskGraphics.FillPath($taskGradient, $taskShape)
$taskRim = [Drawing.Pen]::new([Drawing.Color]::FromArgb(90, 132, 176, 213), .7)
$taskGraphics.DrawPath($taskRim, $taskShape)

$taskCyan = [Drawing.ColorTranslator]::FromHtml('#45D6F1')
$taskOrange = [Drawing.ColorTranslator]::FromHtml('#FFB25B')
foreach ($taskArrow in @(@{ X=22; Start=16; End=39; Head=31; Color=$taskCyan }, @{ X=43; Start=48; End=25; Head=33; Color=$taskOrange })) {
    $taskGlow = [Drawing.Pen]::new([Drawing.Color]::FromArgb(35, $taskArrow.Color), 10)
    $taskPen = [Drawing.Pen]::new($taskArrow.Color, 5)
    foreach ($taskStroke in @($taskGlow, $taskPen)) {
        $taskStroke.StartCap = $taskStroke.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $taskStroke.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
        $taskGraphics.DrawLine($taskStroke, [single]$taskArrow.X, [single]$taskArrow.Start, [single]$taskArrow.X, [single]$taskArrow.End)
        $taskPoints = [Drawing.PointF[]]@([Drawing.PointF]::new($taskArrow.X-7, $taskArrow.Head), [Drawing.PointF]::new($taskArrow.X, $taskArrow.End), [Drawing.PointF]::new($taskArrow.X+7, $taskArrow.Head))
        $taskGraphics.DrawLines($taskStroke, $taskPoints)
    }
    $taskGlow.Dispose(); $taskPen.Dispose()
}
$taskGraphics.Dispose(); $taskGradient.Dispose(); $taskRim.Dispose(); $taskShape.Dispose()
$taskFrames = [Collections.Generic.List[byte[]]]::new()
$taskSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
foreach ($taskSize in $taskSizes) {
    $taskBitmap = [Drawing.Bitmap]::new($taskSize, $taskSize)
    $taskCanvas = [Drawing.Graphics]::FromImage($taskBitmap)
    $taskCanvas.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $taskCanvas.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $taskCanvas.DrawImage($taskMaster, [Drawing.Rectangle]::new(0, 0, $taskSize, $taskSize))
    $taskStream = [IO.MemoryStream]::new()
    $taskBitmap.Save($taskStream, [Drawing.Imaging.ImageFormat]::Png)
    $taskFrames.Add($taskStream.ToArray())
    if ($taskSize -eq 256) { $taskBitmap.Save((Join-Path $taskRoot 'docs\icon.png'), [Drawing.Imaging.ImageFormat]::Png) }
    $taskCanvas.Dispose(); $taskBitmap.Dispose(); $taskStream.Dispose()
}
$taskMaster.Dispose()
$taskOutput = [IO.File]::Create((Join-Path $taskAssets 'MiniDebit.ico'))
$taskWriter = [IO.BinaryWriter]::new($taskOutput)
try {
    $taskWriter.Write([uint16]0); $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]$taskSizes.Count)
    $taskOffset = 6 + 16 * $taskSizes.Count
    for ($taskIndex = 0; $taskIndex -lt $taskSizes.Count; $taskIndex++) {
        $taskDimension = if ($taskSizes[$taskIndex] -eq 256) { 0 } else { $taskSizes[$taskIndex] }
        $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([byte]$taskDimension)
        $taskWriter.Write([byte]0); $taskWriter.Write([byte]0)
        $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]32)
        $taskWriter.Write([uint32]$taskFrames[$taskIndex].Length); $taskWriter.Write([uint32]$taskOffset)
        $taskOffset += $taskFrames[$taskIndex].Length
    }
    foreach ($taskFrame in $taskFrames) { $taskWriter.Write($taskFrame) }
}
finally { $taskWriter.Dispose(); $taskOutput.Dispose() }
