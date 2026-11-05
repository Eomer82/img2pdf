# Img2Pdf — High-Quality Image to PDF Converter

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/Eomer82/img2pdf)](https://github.com/Eomer82/img2pdf/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D4.svg)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%209.0%20%7C%208.0-512BD4.svg)](https://dotnet.microsoft.com/)

A modern, fast, and lightweight Windows desktop application for lossless conversion of **JPG**, **PNG**, **BMP**, **WebP**, and **TIFF** images into high-quality PDF files. Built with **.NET** and **WPF** featuring native **Windows 11 Fluent Design (Mica effect)**, dark mode, live thumbnail previews, drag-and-drop reordering, Quick Look preview overlay, multi-image grids (N-Up), PDF metadata editing, and built-in multi-language localization.

---

## ✨ Features (v1.2.0)

- 🚀 **Broad Image Format Support:**
  - **JPG / JPEG:** Embedded directly into the PDF's `DCTDecode` stream without re-compression. Zero loss of sharpness, metadata, or color accuracy.
  - **PNG & BMP:** Losslessly compressed using `FlateDecode` (Deflate/ZIP) streams.
  - **WebP & TIFF / TIF:** Full support for modern web imagery and multi-page scanned documents.
- 🔀 **Drag-and-Drop Row Reordering:**
  - Intuitively click and drag rows up or down in the file list to reorder pages in your merged PDF.
- 🔍 **Full-Size "Quick Look" Image Preview Overlay:**
  - Double-click any image or tap the **Space** bar to launch an instant full-screen preview modal with dark backdrop.
  - Flip between images with Left / Right arrow keys, rotate with **R**, and close with **Esc** or **Space**.
  - Displays real-time resolution, file size, and current rotation angle.
- 📑 **Multiple Images per Page (N-Up / Receipt Mode):**
  - Arrange **1, 2, or 4 images per page** in a balanced grid layout (2x2) with proportional scaling. Ideal for receipts, document scans, photos, and photo-albums.
- 📝 **PDF Document Metadata:**
  - Embed custom **Document Title**, **Author**, and **Subject / Topic** directly into the generated PDF's standard document info dictionary.
- 📄 **Direct PDF Open Option:**
  - Automatically launch the newly generated PDF file in your default PDF reader immediately upon conversion completion.
- 🔄 **Smart Orientation & Live Rotation:**
  - **Automatic EXIF Orientation:** Phone camera photos (iPhone / Android) are automatically detected and oriented right-side-up.
  - **Manual 90° Rotation:** Rotate any selected image on-the-fly with live visual preview in the file list and preview modal.
- 🖼️ **Live Thumbnail Previews:**
  - Smooth asynchronous thumbnail generation with rounded corners and real-time rotation in the queue.
- 📊 **Interactive Column Sorting:**
  - Click any column header (**File Name**, **Resolution**, **Size**, **Status**, **Folder**) to sort ascending or descending.
- 🗜️ **Flexible Quality & Compression Options:**
  - **100% Lossless Mode:** Bit-exact preservation of original images.
  - **Compressed / Email Mode:** Adjustable JPEG quality slider (50%–95%) to shrink large camera photos down for easy email attachments.
- 📁 **Flexible Batch Processing:**
  - Drag and drop individual images or entire folder trees recursively.
  - **Separate Mode:** Generates one distinct PDF per image (retaining source file names).
  - **Merged Mode:** Combines all selected images into a single, organized multi-page PDF document.
- 📐 **Page Layout & Margin Controls:**
  - **1:1 Original Size:** Each PDF page automatically takes the exact pixel dimensions and DPI of the image.
  - **A4 / US Letter:** Proportional scaling with customizable margins:
    - *0 mm (Borderless / Edge-to-edge)*
    - *10 mm (Compact margin)*
    - *20 mm (Standard document margin)*
- 🔒 **Security & Encryption:**
  - Optional AES password protection to safeguard sensitive scans and documents.
- 🌐 **Multi-Language Support (i18n):**
  - Switch on-the-fly between **English (🇬🇧)**, **Norsk (🇳🇴)**, **Deutsch (🇩🇪)**, and **Español (🇪🇸)** directly from the title bar.
- 🎨 **Modern Windows 11 Fluent UI:**
  - Native Mica backdrop effect, rounded geometry, and full Dark / Light mode toggle.
  - Lightweight resource footprint and non-blocking parallel processing.

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

## 🇳🇴 Norsk Sammendrag (v1.2.0)

Img2Pdf er et raskt og moderne Windows-verktøy for å konvertere bilder (**JPG, PNG, BMP, WebP og TIFF**) til PDF. Nyheter i versjon 1.2.0:
- **Dra og slipp-sortering:** Dra elementer direkte i listen for å endre rekkefølge på PDF-sidene.
- **Fullskjerms Hurtigvisning (Quick Look):** Dobbeltklikk eller trykk Mellomrom for å forhåndsvise bildet i full størrelse med mørk bakgrunn, piltaster for å bla, og R for å rotere.
- **Flere bilder per side:** Plasser 1, 2 eller 4 bilder per side (2x2 rutenett / kvitteringsmodus).
- **PDF-metadata:** Legg inn tittel, forfatter og emne/beskrivelse direkte i PDF-dokumentet.
- **Åpne PDF direkte:** Valg om å åpne den ferdige PDF-filen umiddelbart i din standard PDF-leser.
- **Flerspråklig støtte:** Nå med støtte for **Norsk (🇳🇴)**, **Engelsk (🇬🇧)**, **Tysk (🇩🇪)** og **Spansk (🇪🇸)**.
- **Kamerabilder og EXIF-støtte:** Full støtte for store mobilbilder (50+ MP) med tapsfri rotering og minneoptimalisering.

---

## 📄 License

This project is licensed under the terms of the [MIT License](LICENSE).
