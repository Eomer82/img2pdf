@echo off
chcp 65001 >nul
echo Building Img2Pdf for Windows x64 (Self-Contained Single File)...
echo.

dotnet publish src\Img2Pdf.App\Img2Pdf.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================================
    echo Build successful!
    echo Output binary: publish\Img2Pdf.exe
    echo Standalone executable - no .NET runtime required on target PC.
    echo ============================================================
) else (
    echo.
    echo Build failed. Please verify that .NET SDK is installed.
)
pause
