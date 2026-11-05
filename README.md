# Img2Pdf — High-Quality Image to PDF Converter

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/Eomer82/img2pdf)](https://github.com/Eomer82/img2pdf/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D4.svg)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%209.0%20%7C%208.0-512BD4.svg)](https://dotnet.microsoft.com/)

A modern, fast, and lightweight Windows desktop application for lossless conversion of **JPG**, **PNG**, and **BMP** images into high-quality PDF files. Built with **.NET** and **WPF** featuring native **Windows 11 Fluent Design (Mica effect)**, dark mode support, and built-in multi-language localization.

---

## ✨ Features

- 🚀 **100% Lossless Image Quality:**
  - **JPG / JPEG:** Embedded directly into the PDF's `DCTDecode` stream without re-compression. Zero loss of sharpness, metadata, or color accuracy.
  - **PNG & BMP:** Losslessly compressed using `FlateDecode` (Deflate/ZIP) streams.
- 📁 **Flexible Batch Processing:**
  - Drag and drop individual images or entire folder trees recursively.
  - **Separate Mode:** Generates one distinct PDF per image (retaining source file names).
  - **Merged Mode:** Combines all selected images into a single, organized multi-page PDF document.
- 📐 **Page Layout Options:**
  - **1:1 Original Size:** Each PDF page automatically takes the exact pixel dimensions and DPI of the image (ideal for archiving and graphic design).
  - **A4 / US Letter:** Proportional scaling to standard document sizes with clean margins (auto portrait/landscape orientation).
- 🔒 **Security & Encryption:**
  - Optional AES password protection to safeguard sensitive scans and documents.
- 🌐 **Multi-Language Support (i18n):**
  - Switch on-the-fly between **English** and **Norwegian (Norsk)** without restarting.
  - Extensible architecture designed to easily add new language packs.
- 🎨 **Modern Windows 11 Fluent UI:**
  - Native Mica backdrop effect, rounded geometry, and full Dark / Light mode toggle.
  - Lightweight resource footprint (typically under 50–70 MB RAM).
  - Responsive multithreaded processing (`Parallel.ForEachAsync`) ensuring the interface never freezes.

---

## 💻 Requirements

- **Operating System:** Windows 10 (version 1809 or higher) or Windows 11.
- **Runtime:** Self-contained executable — **no prior .NET installation required** for end users.
- **For Building from Source:** [.NET SDK](https://dotnet.microsoft.com/download) (version 10, 9, or 8).

---

## 🚀 Building and Publishing

### 1. Run in Development Mode
Clone the repository and run:
```bash
dotnet run --project src/Img2Pdf.App
```
Or open `Img2Pdf.sln` in **Visual Studio 2022 / 2026** or **JetBrains Rider** and press **F5**.

### 2. Build Self-Contained Single-File Executable
Use the provided script in Command Prompt (CMD):
```cmd
publish.bat
```
Or in PowerShell:
```powershell
./publish.ps1
```

This compiles a fully optimized, compressed, self-contained single file:
```
publish\Img2Pdf.exe
```
All native DirectX/WPF libraries (`wpfgfx_cor3.dll`, etc.) are bundled inside, allowing it to run standalone on any compatible Windows PC.

---

## 🌍 Adding New Languages

Adding a new language is simple:
1. Open `src/Img2Pdf.App/Services/LocalizationService.cs`.
2. Add a new `LanguageInfo` to `SupportedLanguages`:
   ```csharp
   new("de", "Deutsch", "German", "🇩🇪")
   ```
3. Add translations to the `_translations` dictionary map. The language will automatically appear in the UI language menu!

---

## 🇳🇴 Norsk Sammendrag

Img2Pdf er et raskt og moderne Windows-verktøy for å konvertere bilder (JPG, PNG, BMP) til PDF uten tap av kvalitet. Appen støtter både enkelvise PDF-er og sammenslåing til ett flersidig dokument, passordbeskyttelse, automatisk A4/Letter-tilpasning og moderne Windows 11-design (Mica/mørk modus). Språk kan veksles direkte i appen mellom norsk og engelsk.

---

## 📄 License

This project is licensed under the terms of the [MIT License](LICENSE).
