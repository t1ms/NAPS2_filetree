# NAPS2_filetree

A personal fork of NAPS2 (a document scanning app) that adds OCR-based file naming, document grouping, a hot folder and local search, shipped as a portable Windows ZIP.

## Product

**Upstream**:
The original NAPS2 project this fork was copied from (version 8.2.1). This fork does not take updates from it.
_Avoid_: NAPS2 (when you mean the original project), parent

**Portable ZIP build**:
The only shipped form of the app: a Windows folder you unzip and run, which keeps all its settings in its own `Data` folder.
_Avoid_: portable exe, installer

**Scanner sharing**:
An upstream feature that let the app act as a network scanner for other computers. Removed from this fork.
_Avoid_: eSCL (on its own, since eSCL also covers connecting to network scanners, which stays)

**Network scanner**:
A scanner the app reaches over the local network (eSCL), as opposed to one plugged in by USB.

## Reading text from scans

**OCR zone**:
A rectangle on a page where the app reads text or a barcode.
_Avoid_: region, box, area

**Zone template**:
A named, saved set of OCR zones for one kind of document.
_Avoid_: profile, layout

**Field**:
One named value read from an OCR zone, such as `Invoice Number`.
_Avoid_: token (a token is the placeholder, not the value)

**Zone token**:
A filename placeholder like `$(Invoice Number)` that is replaced by a field's value when saving.
_Avoid_: tag, variable

**Field log**:
The CSV file written next to a saved document, listing the fields read from it.
_Avoid_: export, report

## Organising and saving

**Document group**:
A set of pages in the page list that will be saved as one PDF, named by its document name.
_Avoid_: group (on its own), batch

**Document name**:
The name a user gives a document group, used as its PDF filename.
_Avoid_: index field (the code's name for it)

**Hot folder**:
A watched folder: any PDF or image dropped in it is processed and saved automatically while the app is open.
_Avoid_: watch folder (that's the folder itself, see below), drop folder

**Watch folder**:
The folder the hot folder monitors for new files.

**Destination folder**:
Where the hot folder saves processed files. Must be outside the watch folder.
_Avoid_: output folder

**Search index**:
The local database of text from saved PDFs, used by the app's search window.
_Avoid_: search database
