# PowerShell skript for å bygge Img2Pdf til en enkeltstående .exe-fil
Write-Host "Bygger Img2Pdf som selvstendig Windows-applikasjon..." -ForegroundColor Cyan

$projectPath = "src/Img2Pdf.App/Img2Pdf.App.csproj"
$outputPath = "./publish"

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outputPath

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBygging fullført!" -ForegroundColor Green
    Write-Host "Kjørbar fil ligger i: $outputPath/Img2Pdf.exe" -ForegroundColor Yellow
} else {
    Write-Host "`nBygging feilet. Kontroller at .NET SDK er installert." -ForegroundColor Red
}
