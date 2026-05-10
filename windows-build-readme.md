# Windows Build Guide for NAPS2

This guide provides step-by-step instructions on how to build and run the NAPS2 application on a Windows machine.

## Prerequisites

Before building NAPS2, you must ensure your Windows development environment has the required dependencies installed:

1. **.NET 8.0 SDK**
   - Download the latest .NET 8.0 SDK from Microsoft: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
   - You need the **SDK**, not just the Runtime.

2. **Visual Studio 2022 (Optional but Recommended)**
   - Download Visual Studio 2022 Community (or Professional/Enterprise).
   - During the installation, make sure to select the **.NET desktop development** workload.

3. **Git (Optional)**
   - Required if you are cloning the repository using Git.
   - Download from https://git-scm.com/download/win

## Step-by-Step Build Instructions

You can build the project either using Visual Studio or directly from the Command Line/PowerShell.

### Method 1: Using the Command Line (No Visual Studio Required)

This is the fastest method and only requires the .NET 8 SDK.

1. **Open PowerShell or Command Prompt** as Administrator.
2. **Navigate to the root directory** of the NAPS2 project where the main solution (`.sln`) or the project folders are located.
   ```powershell
   cd C:\path\to\NAPS2
   ```
3. **Build the WinForms Project** by running the following command:
   ```powershell
   dotnet build NAPS2.App.WinForms/NAPS2.App.WinForms.csproj -c Release
   ```
   *Note: This command automatically restores all NuGet packages and compiles the cross-platform `NAPS2.Lib` along with the Windows-specific UI wrapper.*
4. **Run the Application**:
   ```powershell
   dotnet run --project NAPS2.App.WinForms/NAPS2.App.WinForms.csproj -c Release
   ```

### Method 2: Using Visual Studio 2022

1. **Open Visual Studio 2022**.
2. Click **Open a project or solution**.
3. Navigate to your project folder and select the **`NAPS2.sln`** file (or if there is no solution file, you can open `NAPS2.App.WinForms.csproj` directly).
4. In the **Solution Explorer** on the right side, locate the **`NAPS2.App.WinForms`** project.
5. **Right-click** on `NAPS2.App.WinForms` and select **"Set as Startup Project"**.
6. At the top of the window, change the build configuration dropdown from `Debug` to `Release`.
7. Press **F5** (or click the green "Start" button) to build and launch the application.

## Where are the built files?

Once the build is successful, you can find the executable files ready to be distributed or run manually in the following directory:

`NAPS2.App.WinForms\bin\Release\net8.0-windows\`

Look for the `NAPS2.exe` file inside this folder. **Important Note:** By default, you CANNOT just copy the `NAPS2.exe` file by itself to another computer. It relies on all the `.dll` dependency files generated in that same folder. If you want to move the app to another computer, you must copy the **entire contents** of the `net8.0-windows` folder.

## How to create a Single Standalone .exe (No Dependencies)

If you want a single, portable `.exe` file that you can copy-paste to any computer without dragging around a folder full of `.dll` files, you need to "publish" the app instead of just building it.

Run this command in PowerShell from the project root:

```powershell
dotnet publish NAPS2.App.WinForms/NAPS2.App.WinForms.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### What this command does:
- `-r win-x64`: Targets 64-bit Windows specifically.
- `--self-contained true`: Bundles the .NET runtime directly inside the app, so the target computer doesn't even need to have .NET 8 installed.
- `-p:PublishSingleFile=true`: Packages all `.dll` dependencies into one single `NAPS2.exe` file.

You will find your standalone `.exe` file here:
`NAPS2.App.WinForms\bin\Release\net8.0-windows\win-x64\publish\`

You can copy-paste this specific `NAPS2.exe` anywhere, to any Windows computer, and it will run independently!

## Troubleshooting

- **Target Framework Error**: If you get an error complaining about a missing SDK or mismatched target framework, ensure you specifically have the **.NET 8.0 SDK** installed, and try running `dotnet clean` before building again.
- **Missing Packages**: If the build fails because it cannot find certain dependencies, run `dotnet restore` in the project root to force a download of all necessary NuGet packages.
