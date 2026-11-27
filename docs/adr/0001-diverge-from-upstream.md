# This fork does not follow upstream NAPS2

NAPS2_filetree is a single-user fork that ships only as a portable Windows ZIP. We decided not to pull future upstream NAPS2 releases, so we can remove features freely: the update checker, email, scanner sharing, and the local AI model are taken out instead of kept in step with upstream.

## Considered Options

- **Stay mergeable with upstream:** keep changes small and leave unused features in place. Rejected because the unused features carry most of the audit's security and crash findings, and keeping them only to ease merges that we don't plan to do costs more than it saves.

## Consequences

- Upstream bug and security fixes have to be copied in by hand, if at all.
- The update checker is disabled, because accepting an upstream update would overwrite this fork.
- `NAPS2.Escl.Server` stays in the repo only because the SDK tests use it as a fake scanner. The app no longer references it.
- The AI filename tokens (`DOC_DATE`, `DOC_SENDER`, `DOC_TYPE`, `DOC_REF`) are removed together with the AI model.
- Email is removed from the app, but its plumbing inside `NAPS2.Sdk` and the 32-bit scanner worker (MAPI) stays. The worker is the TWAIN scanning path, and changing it can't be checked without a scanner. Nothing in the app reaches that code any more.
- `SixLabors.ImageSharp` stays at 3.1.11 despite six known advisories. The fixed 4.x line requires a Six Labors license key for Release builds, and the package is used only by `NAPS2.Sdk.Tests`, never by the shipped app. Revisit if ImageSharp ever enters the app.
