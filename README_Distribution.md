# ?? Kingdom Hearts Custom Music Patcher

## ? Single Executable Solution

This application now supports **true single-file distribution** with embedded resources!

### ?? What's Included in the .exe:

- ? **Complete .NET runtime** - No installation required
- ? **All Excel configuration files** - Automatically extracted
- ? **Audio processing libraries** - Built-in support for WAV/MP3/MP4
- ? **User interface** - Full WPF application
- ? **Embedded tools** - If available during build

### ??? Building with Embedded Tools:

To create a **completely self-contained** executable:

1. **Obtain the required tools:**
   ```
   utils/
   ??? SingleEncoder/
   ?   ??? SingleEncoder.exe
   ?   ??? SingleEncoder.dll
   ?   ??? SingleEncoder.runtimeconfig.json
   ?   ??? original.scd
   ?   ??? tools/
   ?       ??? adpcmencode3/adpcmencode3.exe
   ?       ??? oggenc/oggenc.exe
   ??? KHPCPatchManager.exe
   ```

2. **Build the application:**
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

3. **Result:** A single `KingdomHeartsCustomMusic.exe` file (~150MB) that contains everything!

### ?? User Experience:

#### **Scenario 1: Complete Build (Recommended)**
- **User downloads:** Just `KingdomHeartsCustomMusic.exe`
- **User runs:** Double-click the .exe
- **Everything works:** Music configuration, patch generation, patch application
- **Tools are extracted automatically** to temporary folders when needed

#### **Scenario 2: Partial Build**
- **User downloads:** Just `KingdomHeartsCustomMusic.exe`
- **User runs:** Application opens and works for music configuration
- **Missing tools:** Clear instructions on what to download and where to get it
- **Upgrade path:** User can obtain tools and rebuild for complete functionality

### ?? Key Features:

- **?? Automatic extraction** - Tools extracted on-demand to temp folders
- **?? Cleanup** - Temporary files cleaned up on application exit
- **? Performance** - Tools only extracted once per session
- **?? Clear messaging** - User knows exactly what's missing and how to get it
- **??? Fallback handling** - App works partially even without all tools

### ?? For Developers:

The application uses `EmbeddedResource` build actions for all tools, with runtime extraction to temporary directories. This approach:

- Maintains single-file distribution
- Provides executable permissions for extracted tools
- Handles missing tools gracefully
- Cleans up resources automatically

### ?? Distribution Options:

1. **Full Package:** Single .exe with all tools embedded (~150MB)
2. **Lite Package:** Single .exe with instructions for tool setup (~100MB)
3. **Development:** Source code for users who want to build their own

---
*The perfect balance between convenience and functionality!* ??