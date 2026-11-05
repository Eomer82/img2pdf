@echo off
chcp 65001 >nul
echo Bygger Img2Pdf for Windows x64 som selvstendig enkeltfil...
echo.

dotnet publish src\Img2Pdf.App\Img2Pdf.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================================
    echo Bygging fullført!
    echo Ferdig programfil: publish\Img2Pdf.exe
    echo Ingen .NET runtime kreves på måldatamaskinen.
    echo ============================================================
) else (
    echo.
    echo Feil oppstod under bygging. Sjekk at .NET SDK er installert.
)
pause
