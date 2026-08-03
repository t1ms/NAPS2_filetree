# NAPS2 — Not Another PDF Scanner

## Project Overview

NAPS2 is an open-source, cross-platform document scanning application written in C# / .NET 9. It supports Windows (WinForms), macOS (native), and Linux (GTK), targeting scanners via WIA, TWAIN, SANE, and eSCL/AirScan drivers.

### Stack
- **Language:** C# / .NET 9 (Windows), .NET 8 (macOS)
- **UI frameworks:** Eto.Forms (shared), WinForms (Windows), GTK (Linux), AppKit (macOS)
- **Key libs:** PdfSharp, Pdfium, Tesseract OCR, ZXing.Net (barcodes), gRPC (worker IPC)
- **Solution file:** `NAPS2.sln` (root); `NAPS2.App.Mac/NAPS2.App.Mac.sln` (macOS)

### Key Projects
| Project | Purpose |
|---|---|
| `NAPS2.Sdk` | Core scanning SDK (drivers, image pipeline, OCR, PDF) |
| `NAPS2.Lib` | Desktop business logic, UI orchestration, profiles |
| `NAPS2.Lib.WinForms` | Windows platform integration |
| `NAPS2.App.WinForms` | Windows GUI executable (net9-windows) |
| `NAPS2.App.Console` | Windows CLI executable |
| `NAPS2.App.Worker` | 32-bit TWAIN worker (single-file, win-x86) |
| `NAPS2.App.PortableLauncher` | Portable bootstrapper / updater launcher |
| `NAPS2.Images` | Cross-platform image abstraction layer |
| `NAPS2.Escl` | eSCL/AirScan protocol (LAN-free, USB) |

---

## Building a Portable Windows ZIP (no installer needed)

### Cross-compiling on Replit (Linux) — verified working
Requires .NET 9 SDK (installed via Replit module `dotnet-9.0`; `global.json` pinned to `9.0.100` with `rollForward: latestFeature`).

```bash
dotnet publish NAPS2.App.WinForms -r win-x64 -c Release --self-contained true /p:DebugType=None /p:DebugSymbols=false
dotnet publish NAPS2.App.Worker -c Release /p:DebugType=None /p:DebugSymbols=false   # win-x86 single-file 32-bit TWAIN worker
```

Then assemble: copy the WinForms publish dir to `App/`, drop `NAPS2.Worker.exe` next to `NAPS2.exe`, add an empty `Data/` folder alongside `App/`, and zip. `_win32/twaindsm.dll`, `_win64/twaindsm.dll`, appsettings.xml, Pdfium and Tesseract all come through the publish output automatically. `NAPS2.Portable.exe` (net462 launcher) cannot be built on Linux — run `App/NAPS2.exe` directly.

Output produced: `naps2-portable-win64.zip` (repo root).

### Official tooling (Windows only)

The official portable package is produced by NAPS2.Tools:

```bash
dotnet run --project NAPS2.Tools -- pkg zip -p win64 --nosign
```

Output: `NAPS2.Setup/publish/<version>/naps2-<version>-win64.zip`

The ZIP contains:
- `NAPS2.Portable.exe` — the launcher (no install required)
- `App/NAPS2.exe` — the main application
- `App/NAPS2.Worker.exe` — 32-bit TWAIN worker
- Native libraries (Pdfium, Tesseract, scanner drivers)

**Alternatively**, a single self-contained EXE publish (experimental):
```bash
dotnet publish NAPS2.App.WinForms/NAPS2.App.WinForms.csproj \
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Output: `NAPS2.App.WinForms/bin/Release/net9-windows/win-x64/publish/NAPS2.exe`

> **Note:** The ZIP approach is more reliable — the single-file EXE still needs the Worker.exe alongside it for 32-bit TWAIN scanners.

---

## Feature Comparison: NAPS2 vs Kodak CapturePro

See conversation for the full gap analysis.

---

## User Preferences
- Goal: portable standalone EXE (no admin/install rights needed)
- Only basic scanning features required — no network/enterprise features
- Primary comparison baseline: Kodak CapturePro
