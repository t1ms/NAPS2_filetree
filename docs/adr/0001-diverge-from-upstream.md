# This fork does not follow upstream NAPS2

NAPS2_filetree is a single-user fork that ships only as a portable Windows ZIP. We decided not to pull future upstream NAPS2 releases, so we can remove features freely: the update checker, email, scanner sharing, and the local AI model are taken out instead of kept in step with upstream.

## Considered Options

- **Stay mergeable with upstream:** keep changes small and leave unused features in place. Rejected because the unused features carry most of the audit's security and crash findings, and keeping them only to ease merges that we don't plan to do costs more than it saves.

## Consequences

- Upstream bug and security fixes have to be copied in by hand, if at all.
- The update checker is disabled, because accepting an upstream update would overwrite this fork.
- `NAPS2.Escl.Server` stays in the repo only because the SDK tests use it as a fake scanner. The app no longer references it.
- The AI filename tokens (`DOC_DATE`, `DOC_SENDER`, `DOC_TYPE`, `DOC_REF`) are removed together with the AI model.
