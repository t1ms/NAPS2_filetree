# NAPS2 Filetree Features Guide

This guide explains the features added to this NAPS2 build and where to find them
in the Windows portable application.

## Start the portable application

1. Download `naps2-portable-win64.zip` from this repository.
2. Extract the **entire** ZIP file.
3. Open:

   ```text
   NAPS2-Portable\App\NAPS2.exe
   ```

Do not copy only `NAPS2.exe`. The application needs the other files in the `App`
folder, including the scanner worker and native PDF/OCR libraries.

---

## Basic scan test

1. Connect and turn on the Kodak scanner.
2. Open NAPS2.
3. Select or create a scan profile.
4. Choose the Kodak device and the TWAIN driver.
5. Scan a small batch first.

The scan pages should appear in the main page list. The list thumbnails use a
background renderer and now fall back to local rendering if the worker has a
problem. Double-click a page to open the larger preview.

---

## 1. Auto-rotate sideways or upside-down pages

There are two ways to use orientation correction.

### Automatically rotate every scan

1. Select the scan profile in the profile panel.
2. Open **Edit Profile**.
3. Click **Advanced**.
4. Turn on:

   ```text
   Auto rotate (detect text orientation)
   ```

5. Save the profile.

New scans using that profile will use Tesseract orientation detection and rotate
pages when the detected direction is reliable.

### Rotate pages already in the page list

1. Select one or more pages.
2. Open the **Rotate** menu.
3. Choose:

   ```text
   Auto Rotate (Detect Orientation)
   ```

The operation is undoable. Use the normal Undo command if a page was changed
incorrectly.

### If auto-rotate is unavailable

Orientation detection needs the Tesseract OCR orientation component. Open the
profile's Advanced settings and allow NAPS2 to download the required OCR
component when prompted.

---

## 2. Create OCR field zones

OCR field zones let you define fields such as vendor, invoice number, date, or
total in a consistent document layout.

### Create a zone template

1. Scan or import a representative page.
2. Select the page.
3. Open the **Image** menu.
4. Choose:

   ```text
   OCR Field Zones
   ```

5. Draw a rectangle around a field.
6. Select the zone and give it a name, for example:

   ```text
   Vendor
   InvoiceNumber
   InvoiceDate
   Total
   ```

7. Add more zones as needed.
8. Give the template a name.
9. For each selected zone, choose **Printed text** for normal OCR or **Barcode**
   and select a barcode format. Use **Any format** when the barcode type is not
   known. Barcode values use the same field results, CSV, and filename
   placeholders as OCR text.
10. Leave this enabled if the template should run automatically:

   ```text
   Use this template for scans (extract fields from each scanned page)
   ```

11. Click **Save**. The dialog explains what to fix if the template name or a
   field name is missing, names are duplicated, or a drawn rectangle is not
   usable. Renaming a field is saved when you save the template.

Zones are stored as page-relative positions, so the same template can be used
on pages with different resolutions.

### Extract fields manually

1. Select one or more pages.
2. Open **Image → Extract Fields**.
3. Wait for OCR to finish.

### View or export field results

Open:

```text
Image → Extracted Field Results
```

This shows the extracted values and provides CSV export.

---

## 3. Optional local AI cleanup

The local AI model cleans noisy OCR values. It is optional and is not required
for scanning or OCR.

### Choose and enable a model

1. Open a page with an OCR field template.
2. Open **Image → OCR Field Zones**.
3. Enable:

   ```text
   Clean up field values with a local AI model (CPU-only, .gguf)
   ```

4. Click **Choose Model...**.
5. Select a compatible `.gguf` model.
6. Save the template.

You can also place a `.gguf` file in the application's local models folder
shown by the dialog.

No model is bundled or downloaded automatically. If no model is available,
NAPS2 keeps the original OCR result instead of preventing the scan.

### Per-field prompts

Select a zone and enter an optional prompt. The special placeholder
`{FieldType}` is replaced with the zone name.

Use prompts for fields that need specific cleanup, for example:

```text
Return only the invoice number, with no label or explanation.
```

Small CPU-only models are useful for simple cleanup but can be less accurate on
complex documents.

---

## 4. Content-based automatic filenames

Content-based filenames are configured through a profile's Auto Save settings.

### Open Auto Save settings

1. Select a scan profile.
2. Click **Edit Profile**.
3. Enable **Auto Save**.
4. Click the Auto Save settings link/button.
5. In the filename field, click **Placeholders**.

The placeholder window shows date/time placeholders and document-content
placeholders with example preview values.

### Built-in date and numbering placeholders

Examples:

```text
$(YYYY)-$(MM)-$(DD)_$(n).pdf
$(YYYY)-$(MM)-$(DD)_$(hh)-$(mm).pdf
```

Common built-in tokens include:

| Token | Meaning |
|---|---|
| `$(YYYY)` | Four-digit year |
| `$(YY)` | Two-digit year |
| `$(MM)` | Two-digit month |
| `$(DD)` | Two-digit day |
| `$(hh)` | Hour |
| `$(mm)` | Minute |
| `$(ss)` | Second |
| `$(n)` / `$(nn)` | Incrementing file number |

### OCR field placeholders

If your active zone template contains `Vendor` and `InvoiceNumber`, use:

```text
$(Vendor)_$(InvoiceNumber).pdf
```

The older syntax is also accepted:

```text
{Vendor}_{InvoiceNumber}.pdf
```

### Generic document placeholders

These can be filled by whole-page local AI extraction when a compatible model
is enabled:

```text
$(DOC_DATE)
$(DOC_SENDER)
$(DOC_TYPE)
$(DOC_REF)
```

Example:

```text
$(DOC_DATE)_$(DOC_SENDER)_$(DOC_TYPE)_$(DOC_REF).pdf
```

If a value is missing, NAPS2 uses a safe fallback such as `Unknown`. Unsafe
Windows filename characters are removed, long values are shortened, and
existing filenames receive a numeric suffix instead of being overwritten.

---

## 5. Search scanned documents

Saved PDF/image documents are added to the local full-text search index when
the save path supports indexing.

Open:

```text
Image → Search Scanned Documents
```

You can also use the keyboard shortcut:

```text
Ctrl+Shift+F
```

Enter a word or phrase and open a result to inspect the matching document.
Search is local; documents are not uploaded to a cloud service.

### Search notes

- The document must be saved/indexed before it can be found.
- Search quality depends on the PDF text layer or OCR text.
- A scanned image without OCR text may not produce useful search results.
- The current search feature is intended for saved documents, not as a live
  search of unsaved pages in the page list.

---

## 6. Hot Folder automatic processing

Hot Folder mode watches a folder while NAPS2 is open. New PDF and image files
are imported, processed, auto-named, saved, and then moved out of the input
folder.

### Configure Hot Folder

1. Open:

   ```text
   Settings → Hot Folder...
   ```

2. Turn on:

   ```text
   Enable hot folder while NAPS2 is running
   ```

3. Choose a **Watch folder**.
4. Choose a separate **Destination folder**.
5. Choose a processing profile that has Auto Save configured.
6. Click **OK**.

The destination folder must be outside the watch folder. For example:

```text
C:\NAPS2-Test\Inbox\
C:\NAPS2-Test\Output\
```

Do not use this arrangement:

```text
C:\NAPS2-Test\Inbox\
C:\NAPS2-Test\Inbox\Output\
```

The watcher prevents this configuration because it could process its own output
repeatedly.

### Test a drop

Copy a PDF or image into the watch folder. NAPS2 waits for the file to stop
changing before it imports it, so a copier can finish writing the file first.

Successful files move to:

```text
<watch folder>\processed\
```

Failed files move to:

```text
<watch folder>\failed\
```

The activity log is:

```text
<watch folder>\hot-folder.log
```

The Hot Folder settings dialog displays the active status and processed/failed
counts.

Hot Folder mode runs only while NAPS2 is open. It is not a Windows background
service.

---

## 7. Thumbnail and preview behavior

The main page list uses smaller thumbnails. The double-click viewer renders the
full page separately.

If the worker cannot render a thumbnail because of a missing runtime file or a
temporary worker error, the list now falls back to in-process rendering.

If thumbnails still appear blank:

1. Close NAPS2.
2. Download the latest `naps2-portable-win64.zip`.
3. Extract it into a new folder rather than mixing files with an older build.
4. Confirm that these files exist:

   ```text
   App\NAPS2.exe
   App\NAPS2.Worker.exe
   App\_win32\
   App\_win64\
   App\runtimes\
   ```

5. Start `App\NAPS2.exe` from the new extraction.

---

## Recommended first test sequence

Use this order to isolate problems:

1. Scan one page and confirm the page appears in the list.
2. Double-click the page and confirm the full preview.
3. Scan a sideways page and test **Rotate → Auto Rotate (Detect Orientation)**.
4. Create a small OCR zone template and use **Image → Extract Fields**.
5. Open **Image → Extracted Field Results** and verify the value.
6. Configure Auto Save with a simple date/number pattern.
7. Save a document and test **Image → Search Scanned Documents**.
8. Configure Hot Folder and copy one existing PDF into it.
9. Add a local GGUF model only after the non-AI path works.

## Current limitations

- The portable package must be tested on Windows; the build environment cannot
  exercise Kodak TWAIN or WinForms rendering directly.
- Local AI is optional and requires a user-provided compatible GGUF file.
- Hot Folder mode stops when NAPS2 closes.
- Complex documents may need a larger local model or carefully written zone
  prompts for reliable field cleanup.