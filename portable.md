# NAPS2 Portable Windows Test Package

This repository contains a portable Windows build for testing. It does not require an
installer or administrator rights.

## What to download

Download:

```text
naps2-portable-win64.zip
```

This is the complete package. Do not copy only `NAPS2.exe`.

## What to copy

1. Copy `naps2-portable-win64.zip` to the Windows computer.
2. Extract the **entire** ZIP file to a normal folder, for example:

   ```text
   C:\Tools\NAPS2-Portable\
   ```

3. Keep the extracted folder structure unchanged:

   ```text
   NAPS2-Portable\
   ├── App\
   │   ├── NAPS2.exe
   │   ├── NAPS2.Worker.exe
   │   ├── _win32\
   │   ├── _win64\
   │   └── runtimes\
   └── Data\
   ```

The `App` folder contains the main application, PDF/OCR components, scanner drivers,
and the 32-bit TWAIN worker. The `Data` folder is reserved for portable application
data.

## How to start the app

Open:

```text
NAPS2-Portable\App\NAPS2.exe
```

For the first test, launch `NAPS2.exe` directly. The older `NAPS2.Portable.exe`
launcher is not included in this Linux-built package.

## Testing the Kodak scanner

1. Connect and power on the Kodak scanner.
2. Start NAPS2.
3. Create or select a profile using the TWAIN driver.
4. Select the Kodak i4250 and scan a small test batch first.
5. Confirm that pages appear correctly before testing OCR, auto-rotation, or auto-save.

The package includes both the 64-bit application and the 32-bit `NAPS2.Worker.exe`
needed for many TWAIN drivers. If the scanner does not appear, check that its Kodak
TWAIN driver is installed on Windows and that no other scanning application is
holding the device.

## Testing OCR and auto-rotation

The application can run basic OCR using its included Tesseract components. Download
the required Tesseract language data from the application's OCR settings if it is not
already present.

Auto-rotation uses the Tesseract orientation data. Enable it in the scan profile's
advanced settings after the OCR language components have been installed.

## Testing content-based filenames

Configure Auto Save in a scan profile and use tokens such as:

```text
$(YYYY)-$(MM)-$(DD)_$(DOC_TYPE)_$(DOC_REF).pdf
```

Zone fields can be used with names from the active OCR zone template:

```text
$(VENDOR)_$(INVOICE_NUMBER).pdf
```

The older `{FieldName}` syntax is also supported.

Missing or unsafe values are replaced with safe fallback text, and existing files
receive numeric collision suffixes.

## Testing the hot folder

1. Open **Settings → Hot Folder**.
2. Choose a watch folder, such as:

   ```text
   C:\NAPS2-Test\Inbox\
   ```

3. Choose a separate destination folder, such as:

   ```text
   C:\NAPS2-Test\Output\
   ```

4. Select a scan profile that has Auto Save configured.
5. Enable the hot folder and save the settings.
6. Copy a PDF or image into the watch folder.
7. Wait for the file to finish copying. NAPS2 waits for the file to become stable
   before processing it.

Successful inputs are moved to:

```text
C:\NAPS2-Test\Inbox\processed\
```

Failed inputs are moved to:

```text
C:\NAPS2-Test\Inbox\failed\
```

The activity log is written to:

```text
C:\NAPS2-Test\Inbox\hot-folder.log
```

The destination folder must be outside the watch folder. This prevents the watcher
from processing its own output repeatedly.

## Local AI model

No AI model is bundled or downloaded automatically. Scanning, OCR, search, naming,
and hot-folder processing work without a model.

If you want to test local AI cleanup or document-field extraction, provide your own
compatible GGUF model in the application's model settings and enable the local AI
features. Model files can be large, so do not place them inside the GitHub ZIP unless
you specifically want to distribute one separately.

## Important testing notes

- Copy the complete extracted `NAPS2-Portable` folder when moving the app to another
  computer.
- Do not rename or remove files inside `App`.
- Start with a few pages before testing a large batch.
- This package was built on Linux for `win-x64`; the application must be tested on
  Windows.
- A real Windows test is still required to confirm Kodak TWAIN communication,
  orientation correction, and copier/hot-folder behavior.