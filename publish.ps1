# Building Img2Pdf for Windows x64 (Self-Contained Single File)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host "Building Img2Pdf for Windows x64..." -ForegroundColor Cyan

dotnet publish src/Img2Pdf.App/Img2Pdf.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o ./publish

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n============================================================" -ForegroundColor Green
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "Output binary: publish/Img2Pdf.exe" -ForegroundColor Yellow
    Write-Host "Standalone executable - no .NET runtime required on target PC." -ForegroundColor Gray
    Write-Host "============================================================" -ForegroundColor Green
} else {
    Write-Host "Build failed. Please verify that .NET SDK is installed." -ForegroundColor Red
}
