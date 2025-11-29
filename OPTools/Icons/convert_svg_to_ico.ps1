# PowerShell script to convert SVG to ICO
# Requires ImageMagick (magick command) or Inkscape

$svgPath = Join-Path $PSScriptRoot "chip_icon.svg"
$icoPath = Join-Path $PSScriptRoot "chip_icon.ico"

# Try ImageMagick first
if (Get-Command magick -ErrorAction SilentlyContinue) {
    Write-Host "Using ImageMagick to convert SVG to ICO..."
    magick convert -background transparent "$svgPath" -define icon:auto-resize=256,128,96,64,48,32,16 "$icoPath"
    if (Test-Path $icoPath) {
        Write-Host "Successfully created ICO file: $icoPath" -ForegroundColor Green
        exit 0
    }
}

# Try Inkscape as fallback
if (Get-Command inkscape -ErrorAction SilentlyContinue) {
    Write-Host "Using Inkscape to convert SVG to ICO..."
    # Inkscape doesn't directly create ICO, so we'll create PNGs first
    $tempDir = Join-Path $PSScriptRoot "temp_icons"
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
    
    $sizes = @(16, 32, 48, 64, 96, 128, 256)
    foreach ($size in $sizes) {
        $pngPath = Join-Path $tempDir "${size}x${size}.png"
        inkscape --export-type=png --export-width=$size --export-height=$size --export-filename="$pngPath" "$svgPath" 2>&1 | Out-Null
    }
    
    # Use ImageMagick to combine PNGs into ICO (if available)
    if (Get-Command magick -ErrorAction SilentlyContinue) {
        $pngFiles = Get-ChildItem -Path $tempDir -Filter "*.png" | Sort-Object { [int]($_.BaseName -replace 'x\d+', '') } -Descending
        $pngList = ($pngFiles | ForEach-Object { $_.FullName }) -join " "
        magick convert $pngList "$icoPath"
        Remove-Item -Recurse -Force $tempDir
        if (Test-Path $icoPath) {
            Write-Host "Successfully created ICO file: $icoPath" -ForegroundColor Green
            exit 0
        }
    }
    
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}

Write-Host "Error: Neither ImageMagick nor Inkscape found." -ForegroundColor Red
Write-Host "Please install ImageMagick from https://imagemagick.org/script/download.php" -ForegroundColor Yellow
Write-Host "Or install Inkscape from https://inkscape.org/release/" -ForegroundColor Yellow
exit 1

