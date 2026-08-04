---
name: Eto WinForms drawing pitfalls
description: Compositing thumbnails via Eto Graphics.DrawImage produced blank list thumbnails on Windows
---

- Recompositing a thumbnail into a fresh Eto `Bitmap` with `Graphics.DrawImage` (to overlay a document-group border) rendered blank white thumbnails (border visible, image missing) on the WinForms backend, while the same source image rendered fine in the full Preview.
- **Why:** unknown Eto/WinForms interaction; the thumbnail render/sync pipeline itself was verified correct (thumbnail changes are reference-compared in ImageRenderState → replace ops → list bitmap regenerated).
- **How to apply:** draw overlays (borders, badges) directly onto the existing Eto image with `new Graphics(existingBitmap)` instead of compositing into a new bitmap; wrap overlay drawing in try/catch that falls back to the plain thumbnail.

- Image-editor overlays must derive their bounds from one symmetric inset and a positive fitted image rectangle; transient zero-sized controls can otherwise produce invalid coordinates during WinForms dialog resizing.
- **Why:** the split editor exposed the shared viewer's asymmetric right/bottom inset and lack of dimension guards as a visibly displaced image/split line.
- **How to apply:** fit the displayed image with `min(availableWidth/imageWidth, availableHeight/imageHeight)`, center it, and skip painting/drag math until both overlay and image dimensions are valid.
