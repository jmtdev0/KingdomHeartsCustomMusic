# Kingdom Hearts Custom Music - Fixed Build Script
# This script creates a complete distribution package with proper file structure

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Kingdom Hearts Custom Music - Build Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Create distribution directory
$distDir = ".\dist"
$releaseDir = ".\dist\KingdomHeartsCustomMusic"

Write-Host "?? Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $distDir) {
    Remove-Item $distDir -Recurse -Force
}
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

Write-Host ""
Write-Host "?? Cleaning project..." -ForegroundColor Yellow
dotnet clean --configuration Release

Write-Host ""
Write-Host "??? Building application..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "?? Publishing application with dependencies..." -ForegroundColor Yellow
dotnet publish --configuration Release --output $releaseDir --verbosity normal

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Publish failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "?? Verifying critical files..." -ForegroundColor Yellow

# Check for critical files
$criticalFiles = @(
    "KingdomHeartsCustomMusic.exe",
    "resources\All Games Track List - KH1.xlsx",
    "resources\All Games Track List - KH2.xlsx",
    "utils\SingleEncoder\SingleEncoder.exe",
    "utils\SingleEncoder\original.scd",
    "utils\SingleEncoder\tools\adpcmencode3\adpcmencode3.exe",
    "utils\SingleEncoder\tools\oggenc\oggenc.exe"
)

$missingFiles = @()
foreach ($file in $criticalFiles) {
    $fullPath = Join-Path $releaseDir $file
    if (-not (Test-Path $fullPath)) {
        $missingFiles += $file
        Write-Host "  ? Missing: $file" -ForegroundColor Red
    } else {
        Write-Host "  ? Found: $file" -ForegroundColor Green
    }
}

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "?? WARNING: Missing critical files!" -ForegroundColor Yellow
    Write-Host "The application may not work correctly without these files:" -ForegroundColor Yellow
    foreach ($file in $missingFiles) {
        Write-Host "  • $file" -ForegroundColor Yellow
    }
    Write-Host ""
}

Write-Host ""
Write-Host "?? Copying additional files..." -ForegroundColor Yellow

# Copy KHPCPatchManager if it exists
if (Test-Path "KHPCPatchManager.exe") {
    Copy-Item "KHPCPatchManager.exe" $releaseDir -Force
    Write-Host "  ? KHPCPatchManager.exe" -ForegroundColor Green
} else {
    Write-Host "  ?? KHPCPatchManager.exe not found (optional)" -ForegroundColor Yellow
}

# Create patches directory
$patchesDir = Join-Path $releaseDir "patches"
New-Item -ItemType Directory -Path $patchesDir -Force | Out-Null
Write-Host "  ? patches\ directory created" -ForegroundColor Green

# Create README for distribution
$readmeContent = @"
# Kingdom Hearts Custom Music v1.0.0

## What's Included

- **KingdomHeartsCustomMusic.exe** - Main application
- **KHPCPatchManager.exe** - Patch application tool (if included)
- **resources/** - Track lists and configuration files
- **utils/SingleEncoder/** - Audio encoding tools
- **patches/** - Generated patch files will appear here

## Quick Start

1. **Generate Custom Music Patches:**
   - Run KingdomHeartsCustomMusic.exe
   - Select your audio files for each track (WAV, MP3, MP4 supported)
   - Click "Generate Patch"
   - Your patch files will be created in the "patches" folder

2. **Apply Patches to Kingdom Hearts:**
   - Click "Select & Apply Patch" to launch KHPCPatchManager
   - Or manually run KHPCPatchManager.exe
   - Select your generated patch file (.kh1pcpatch or .kh2pcpatch)
   - Choose your Kingdom Hearts installation folder
   - Apply the patch and enjoy your custom music!

## System Requirements

- Windows 10/11 (64-bit)
- Kingdom Hearts HD 1.5+2.5 ReMIX (Steam/Epic Games)
- At least 1GB free disk space
- Audio files in WAV, MP3, or MP4 format

## Troubleshooting

### "Encoding Error" - Cannot find file
- Make sure all files in the utils/SingleEncoder/ folder are present
- Check that you have write permissions to the application folder
- Try running as administrator if needed

### "Missing Track List" error
- Ensure the resources/ folder with Excel files is present
- Check that both KH1 and KH2 xlsx files exist

### Audio encoding issues
- Supported formats: WAV, MP3, MP4
- For best results, use WAV files
- Large files may take longer to process

## Support

For issues and support, visit: https://github.com/jmtdev0/KingdomHeartsCustomMusic

## License

This software is provided as-is for educational and modding purposes.
Kingdom Hearts is a trademark of Square Enix.
"@

$readmeContent | Out-File -FilePath "$releaseDir\README.txt" -Encoding UTF8
Write-Host "  ? README.txt created" -ForegroundColor Green

# Get file sizes and create distribution summary
$exeFile = Get-ChildItem "$releaseDir\*.exe" | Where-Object { $_.Name -like "*KingdomHeartsCustomMusic*" } | Select-Object -First 1
$totalSize = (Get-ChildItem $releaseDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB

Write-Host ""
Write-Host "? Build completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "?? Distribution Summary:" -ForegroundColor Cyan
Write-Host "  ?? Location: $((Get-Item $releaseDir).FullName)" -ForegroundColor White
Write-Host "  ?? Main executable: $([math]::Round($exeFile.Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host "  ?? Total package size: $([math]::Round($totalSize, 2)) MB" -ForegroundColor White
Write-Host ""

if ($missingFiles.Count -eq 0) {
    Write-Host "?? All critical files are present!" -ForegroundColor Green
} else {
    Write-Host "??  Some files are missing - check the warnings above" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "?? Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test the executable in the dist folder" -ForegroundColor White
Write-Host "  2. Try generating a patch to verify everything works" -ForegroundColor White
Write-Host "  3. Create a ZIP file for easy distribution" -ForegroundColor White
Write-Host ""

# Ask if user wants to create a ZIP file
$createZip = Read-Host "Do you want to create a ZIP file for distribution? (y/N)"
if ($createZip -eq "y" -or $createZip -eq "Y") {
    $zipPath = ".\dist\KingdomHeartsCustomMusic-v1.0.0.zip"
    Write-Host ""
    Write-Host "?? Creating ZIP package..." -ForegroundColor Yellow
    
    Compress-Archive -Path "$releaseDir\*" -DestinationPath $zipPath -Force
    $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    
    Write-Host "? ZIP package created: $zipSize MB" -ForegroundColor Green
    Write-Host "?? Location: $((Get-Item $zipPath).FullName)" -ForegroundColor White
}

Write-Host ""
Read-Host "Press Enter to exit"