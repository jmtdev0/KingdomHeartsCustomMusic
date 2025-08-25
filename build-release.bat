@echo off
echo ================================================
echo Kingdom Hearts Custom Music - Build Script
echo ================================================
echo.

echo ?? Cleaning previous builds...
dotnet clean --configuration Release

echo.
echo ??? Building application...
dotnet build --configuration Release

echo.
echo ?? Publishing self-contained executable...
dotnet publish --configuration Release --output "./dist" --verbosity normal

echo.
echo ? Build completed!
echo.
echo ?? Output directory: .\dist\
dir ".\dist\*.exe" /B

echo.
echo ?? Your Kingdom Hearts Custom Music application is ready for distribution!
echo ?? Location: %CD%\dist\
echo.
pause