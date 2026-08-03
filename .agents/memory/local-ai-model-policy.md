---
name: Local AI model policy
description: Product decision for how local GGUF models are distributed and enabled.
---

The portable app should not bundle or download a default AI model. Local AI cleanup and document extraction remain optional and disabled until the user supplies and enables a compatible GGUF model.

**Why:** Users prioritize a lightweight, predictable, fully local package and prefer choosing the model that fits their hardware and accuracy needs.

**How to apply:** Keep model setup user-controlled; never add an automatic model download or a bundled model as a prerequisite for scanning, OCR, search, naming, or hot-folder processing.