$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$root=Split-Path -Parent $PSScriptRoot
$shapes=@('M11,0.7 C12,5.5 17.6,6.3 17.6,12.5 C17.6,17.1 14.1,19.3 10,19.3 C5.8,19.3 2.5,16.8 2.5,12.7 C2.5,9.8 4.3,7.3 6.3,5.5 C6.2,8 7.8,9.5 8.8,10 C7.8,5.7 11.2,4 11,0.7 Z','M10.6,10 C11,12 14,13 12.8,16 C11.6,19 7.2,17.8 7.3,15 C7.3,13.1 9.3,12.8 10.6,10 Z')
$colors=@('#F28DB8','#FFDBEB')
$blobs=@()
foreach($size in @(16,24,32,48,64,128,256)){
 $visual=[Windows.Media.DrawingVisual]::new()
 $dc=$visual.RenderOpen()
 $dc.PushTransform([Windows.Media.ScaleTransform]::new($size/20.0,$size/20.0))
 for($i=0;$i -lt 2;$i++){ $brush=[Windows.Media.BrushConverter]::new().ConvertFromString($colors[$i]);$dc.DrawGeometry($brush,$null,[Windows.Media.Geometry]::Parse($shapes[$i])) }
 $dc.Pop();$dc.Close()
 $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($visual)
 $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream=[IO.MemoryStream]::new();$encoder.Save($stream);$blobs+=,@{Size=$size;Data=$stream.ToArray()};$stream.Dispose()
 if($size -eq 256){[IO.File]::WriteAllBytes((Join-Path $root 'src/AscendAPP.Desktop/Assets/FooMeter.png'),$blobs[-1].Data)}
}
$icon=[IO.MemoryStream]::new();$writer=[IO.BinaryWriter]::new($icon)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$blobs.Count)
$offset=6+16*$blobs.Count
foreach($blob in $blobs){$d=if($blob.Size -eq 256){0}else{$blob.Size};$writer.Write([byte]$d);$writer.Write([byte]$d);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$blob.Data.Length);$writer.Write([uint32]$offset);$offset+=$blob.Data.Length}
foreach($blob in $blobs){$writer.Write([byte[]]$blob.Data)}
[IO.File]::WriteAllBytes((Join-Path $root 'src/AscendAPP.Desktop/Assets/FooMeter.ico'),$icon.ToArray());$writer.Dispose();$icon.Dispose()
'Generated original pink flame icons at 7 sizes.'
