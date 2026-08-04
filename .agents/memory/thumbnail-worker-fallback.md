---
name: Thumbnail worker fallback
description: Windows thumbnail rendering must remain usable when the helper process or native dependencies fail.
---

The thumbnail list should fall back to the in-process renderer when the native worker cannot start or fails. The full-image viewer already uses in-process rendering, so relying exclusively on the worker can leave list previews blank while double-click still works.

**Why:** Portable Windows packages and scanner environments can have worker/native-runtime issues that should not make otherwise valid scanned images invisible in the main list.

**How to apply:** Preserve worker rendering as the preferred fast path, but catch worker startup/IPC failures per thumbnail, render locally, and keep logging the worker problem for diagnosis.