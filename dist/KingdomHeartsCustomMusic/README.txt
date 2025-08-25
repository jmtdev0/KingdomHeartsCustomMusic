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
