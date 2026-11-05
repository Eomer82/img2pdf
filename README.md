# Img2Pdf — Høykvalitets Bilde-til-PDF Konverterer

En moderne, rask og lettdrevet Windows-applikasjon for tapsfri konvertering av **JPG**, **PNG** og **BMP** til PDF-filer. Bygget med **.NET 10** (eller .NET 9/8) og **WPF med Windows 11 Fluent Design (Mica)**.

---

## ✨ Funksjoner

- 🚀 **100 % tapsfri bildekvalitet (Maksimal kvalitet):**
  - **JPG / JPEG:** Bygges direkte inn i PDF-ens `DCTDecode`-strøm uten rekomprimering. Ingen tap av skarphet eller farger.
  - **PNG og BMP:** Komprimeres tapsfritt (`FlateDecode`).
- 📁 **Fleksibel Batch-håndtering:**
  - Dra-og-slipp filer eller hele mapper rekursivt rett inn i programmet.
  - **Modus 1:** Én PDF per bilde (bevarer filnavn).
  - **Modus 2:** Slå sammen alle bilder til én felles, flersidig PDF.
- 📐 **Sideoppsett:**
  - **1:1 Original:** PDF-siden får nøyaktig samme dimensjoner som bildet (perfekt for bevaring).
  - **A4 / US Letter:** Automatisk tilpasning til stående/liggende format med marger.
- 🎨 **Moderne brukergrensesnitt:**
  - Ekte Windows 11 Fluent Design (Mica-effekter, avrundede hjørner, mørk/lys modus-veksler).
  - Svært lavt minneforbruk (typisk under 50–70 MB RAM).
  - Sanntids fremdriftsindikator og multithreading (`Parallel.ForEachAsync`) så grensesnittet aldri fryser.

---

## 🛠️ Prosjektstruktur

```
img2pdf/
├── Img2Pdf.sln
├── publish.bat                  # Bygg til selvstendig .exe (CMD)
├── publish.ps1                  # Bygg til selvstendig .exe (PowerShell)
├── README.md
└── src/
    └── Img2Pdf.App/
        ├── Img2Pdf.App.csproj   # Prosjektfil (.NET 10 / net10.0-windows)
        ├── App.xaml / App.xaml.cs
        ├── MainWindow.xaml / MainWindow.xaml.cs
        ├── Models/
        │   ├── ImageFileItem.cs
        │   └── ConversionSettings.cs
        ├── Services/
        │   ├── ImageDiscoveryService.cs
        │   └── PdfConversionService.cs
        └── ViewModels/
            └── MainViewModel.cs
```

---

## 💻 Komme i gang

### Forutsetninger
- Windows 10 (versjon 1809+) eller Windows 11.
- [.NET SDK](https://dotnet.microsoft.com/download) (versjon 10, 9 eller 8).

> **Tips om .NET-versjon:**  
> Standardkonfigurasjonen er satt til `net10.0-windows`. Dersom utviklingsmaskinen din har .NET 9 eller .NET 8 installert, kan du endre `<TargetFramework>` i `Img2Pdf.App.csproj` til:
> ```xml
> <TargetFramework>net9.0-windows</TargetFramework>
> <!-- eller -->
> <TargetFramework>net8.0-windows</TargetFramework>
> ```

---

## 🚀 Kjøre og bygge prosjektet

### 1. Kjøre direkte i utviklermodus
Åpne mappen i terminalen (PowerShell eller CMD) og kjør:
```bash
dotnet run --project src/Img2Pdf.App
```
Eller åpne `Img2Pdf.sln` i **Visual Studio 2022 / 2026** eller **JetBrains Rider** og trykk **F5**.

### 2. Bygge en ferdig, selvstendig `.exe`-fil
Kjør det medfølgende skriptet:
```bash
publish.bat
```
Eller via PowerShell:
```powershell
./publish.ps1
```

Dette produserer en optimalisert, komprimert fil:
```
publish/Img2Pdf.exe
```
Denne filen er **100 % selvstendig** (Self-Contained Single-File) og kan distribueres til andre Windows-maskiner uten at de trenger å installere .NET.
