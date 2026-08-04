---
name: Eto WinForms drawing pitfalls
description: Compositing thumbnails via Eto Graphics.DrawImage produced blank list thumbnails on Windows
---

- Recompositing a thumbnail into a fresh Eto `Bitmap` with `Graphics.DrawImage` (to overlay a document-group border) rendered blank white thumbnails (border visible, image missing) on the WinForms backend, while the same source image rendered fine in the full Preview.
- **Why:** unknown Eto/WinForms interaction; the thumbnail render/sync pipeline itself was verified correct (thumbnail changes are reference-compared in ImageRenderState → replace ops → list bitmap regenerated).
- **How to apply:** draw overlays (borders, badges) directly onto the existing Eto image with `new Graphics(existingBitmap)` instead of compositing into a new bitmap; wrap overlay drawing in try/catch that falls back to the plain thumbnail.
