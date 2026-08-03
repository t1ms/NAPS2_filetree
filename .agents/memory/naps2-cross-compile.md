---
name: NAPS2 cross-compile on Replit
description: Gotchas when building the portable Windows NAPS2 ZIP on Linux/Replit
---

- Grpc.Tools protoc: NAPS2.Sdk/NAPS2.Lib csproj files once had hardcoded `/opt/homebrew/bin/protoc` overrides (from a Mac session) that break Linux builds — the NuGet Grpc.Tools package ships a linux-x64 protoc, so no override is needed. **Why:** MSB6004 "task executable location invalid" during publish. **How to apply:** if that error reappears, grep for `Protobuf_ProtocFullPath` and delete the overrides.
- The Nix `dotnet-9.0` module provides an SDK patch (e.g. 9.0.308) below what upstream global.json pins; set global.json to `9.0.100` + `rollForward: latestFeature`.
- Long `dotnet publish` runs must be launched with `setsid nohup ... & disown` in ShellExec, or the process dies when the shell session ends. First NuGet restore can silently hang; a plain `dotnet restore` retry succeeded quickly.
- Portable ZIP recipe verified: WinForms publish dir → `App/`, add win-x86 single-file `NAPS2.Worker.exe` beside `NAPS2.exe`, empty `Data/` beside `App/`; twaindsm DSMs/Pdfium/Tesseract flow through publish automatically. NAPS2.Portable.exe (net462) can't build on Linux — run App/NAPS2.exe directly.
