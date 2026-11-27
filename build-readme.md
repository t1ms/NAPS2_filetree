# NAPS2 Build Guide

This guide explains how to build NAPS2 from source. NAPS2 is built using .NET 8 and uses Eto.Forms for cross-platform UI.

## Build Requirements

Regardless of your operating system, you will need:
1. **.NET 8.0 SDK** (Download from https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

## Checking changes on an Apple Silicon Mac (no Mac app)

This fork ships only the Windows portable ZIP. To build and run the unit tests on an Apple Silicon Mac without the `macos` workload:

```bash
dotnet build NAPS2.Lib.Tests/NAPS2.Lib.Tests.csproj -c Debug -p:SkipMacTarget=true -p:Protobuf_ProtocFullPath=/opt/homebrew/bin/protoc -p:gRPC_PluginFullPath=/opt/homebrew/bin/grpc_csharp_plugin
dotnet test NAPS2.Lib.Tests/NAPS2.Lib.Tests.csproj -c Debug --no-build
```

- `SkipMacTarget=true` skips the `net8-macos` target, so the `macos` workload isn't needed.
- The protoc paths point at Homebrew's tools (`brew install protobuf grpc`), because the Grpc.Tools package only includes an Intel Mac build of protoc.

## Building on macOS

The macOS build produces a native `.app` bundle.

1. **Open Terminal** and navigate to the project directory:
   ```bash
   cd /path/to/NAPS2
   ```

2. **Build the Application**:
   ```bash
   dotnet build NAPS2.App.Mac/NAPS2.App.Mac.csproj
   ```

3. **Deploy the App Bundle**:
   The build command compiles the code, but macOS requires the application to be placed in an `.app` container to run correctly. Run these commands to copy the newly built app to your Applications folder:
   ```bash
   # Remove any existing build
   sudo rm -rf /Applications/NAPS2.app
   
   # Copy the newly built application
   sudo cp -R "NAPS2.App.Mac/bin/Debug/net8-macos/NAPS2.app" /Applications/NAPS2.app
   ```

4. **Sign the Bundle**:
   macOS requires applications to be cryptographically signed, or the OS will refuse to run them or block certain features (like camera/scanner access).
   ```bash
   sudo codesign --force --deep --sign - --entitlements "NAPS2.App.Mac/dev.entitlements.plist" /Applications/NAPS2.app
   ```

5. **Run the App**:
   ```bash
   open /Applications/NAPS2.app
   ```
   *(Note: You can also just double click it from your Applications folder)*

## Building on Windows

Please see the dedicated `windows-build-readme.md` file located in this directory for detailed instructions on building for Windows (both Command Line and Visual Studio methods), as well as instructions for creating a single standalone portable `.exe` file.

## Building on Linux

NAPS2 provides a GTK-based UI for Linux environments.

1. **Install Prerequisites**:
   You will need the .NET 8 SDK and GTK3 development packages. On Ubuntu/Debian:
   ```bash
   sudo apt install libgtk-3-dev
   ```

2. **Build the Application**:
   ```bash
   dotnet build NAPS2.App.Gtk/NAPS2.App.Gtk.csproj
   ```

3. **Run the Application**:
   ```bash
   dotnet run --project NAPS2.App.Gtk/NAPS2.App.Gtk.csproj
   ```

## Troubleshooting

- **"Could not load library: libpdfium.dylib" (macOS)**: This happens if the app is run directly via `dotnet run` instead of being properly packaged and signed in the `.app` bundle. Always use the deployment and signing commands provided above.
- **Missing Dependencies**: Run `dotnet restore` in the root directory to forcefully download all required NuGet packages.
