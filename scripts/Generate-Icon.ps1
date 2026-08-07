param(
    [Parameter(Mandatory = $true)][string]$Base64Path,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Read-BigEndianUInt32 {
    param([byte[]]$Bytes, [int]$Offset)
    return [uint32](
        ([uint32]$Bytes[$Offset] -shl 24) -bor
        ([uint32]$Bytes[$Offset + 1] -shl 16) -bor
        ([uint32]$Bytes[$Offset + 2] -shl 8) -bor
        ([uint32]$Bytes[$Offset + 3])
    )
}

$encoded = [IO.File]::ReadAllText($Base64Path).Trim()
if ([string]::IsNullOrWhiteSpace($encoded)) { throw 'Icon base64 source is empty.' }
$source = [Convert]::FromBase64String($encoded)

$signature = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A)
$frames = New-Object System.Collections.Generic.List[object]

for ($i = 0; $i -le $source.Length - $signature.Length; $i++) {
    $match = $true
    for ($j = 0; $j -lt $signature.Length; $j++) {
        if ($source[$i + $j] -ne $signature[$j]) { $match = $false; break }
    }
    if (-not $match) { continue }

    $cursor = $i + 8
    $end = -1
    while ($cursor + 12 -le $source.Length) {
        $length = [int](Read-BigEndianUInt32 $source $cursor)
        if ($length -lt 0 -or $cursor + 12 + $length -gt $source.Length) { break }
        $type = [Text.Encoding]::ASCII.GetString($source, $cursor + 4, 4)
        $cursor += 12 + $length
        if ($type -eq 'IEND') { $end = $cursor; break }
    }
    if ($end -lt 0) { continue }

    $pngLength = $end - $i
    $png = New-Object byte[] $pngLength
    [Array]::Copy($source, $i, $png, 0, $pngLength)
    $width = [int](Read-BigEndianUInt32 $png 16)
    $height = [int](Read-BigEndianUInt32 $png 20)
    if ($width -le 0 -or $height -le 0 -or $width -gt 256 -or $height -gt 256) { continue }

    $frames.Add([pscustomobject]@{ Width=$width; Height=$height; Data=$png })
    $i = $end - 1
}

$frames = @($frames | Sort-Object Width, Height -Unique)
if ($frames.Count -eq 0) { throw 'No valid PNG frames were found in the icon source.' }

$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Force $directory | Out-Null }

$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $writer = New-Object IO.BinaryWriter($stream)
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)

    $imageOffset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $writer.Write([byte](if ($frame.Width -ge 256) { 0 } else { $frame.Width }))
        $writer.Write([byte](if ($frame.Height -ge 256) { 0 } else { $frame.Height }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Data.Length)
        $writer.Write([uint32]$imageOffset)
        $imageOffset += $frame.Data.Length
    }

    foreach ($frame in $frames) { $writer.Write($frame.Data) }
    $writer.Flush()
}
finally {
    if ($writer) { $writer.Dispose() } else { $stream.Dispose() }
}

if ((Get-Item $OutputPath).Length -lt 1000) { throw 'Generated ICO is unexpectedly small.' }
Write-Host "Generated valid ICO with $($frames.Count) frame(s): $OutputPath"
