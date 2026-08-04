# Project Security Audit — 2026-08-04

## Scope

This audit assesses the checked-out NAPS2 project and its portable Windows ZIP for indicators described in the supplied advisory about the live npm supply-chain attack. It deliberately excludes GitHub account and organization actions, credentials, tokens, sessions, repository permissions, and GitHub-side settings.

Audited source revision: `0bdff8bc36015d5d727bce54defa3bd884c6f68a`.

## Findings

No indicators of the reported npm attack were found in the project workspace or in either inspected portable ZIP.

- The project has no `package.json`, npm lockfile, Yarn lockfile, pnPM lockfile, `.npmrc`, or Node dependency tree.
- No `setup.mjs` or `Math_Symbol.js` file was found, including generated output directories.
- No tracked source/configuration file contained the advisory indicators: `Shai-Hulud`, `Math_Symbol.js`, `setup.mjs`, `keyv@6.0.0`, `cacheable`, `flat-cache`, or `file-entry-cache`.
- The only matching text in the workspace is the supplied advisory under `attached_assets/`, which was excluded from source findings.
- Checked-in automation is .NET-only: the GitHub Actions workflow uses `actions/checkout` and `actions/setup-dotnet`; the checked-in post-merge script only prints a message. Neither invokes npm, Node, Yarn, pnPM, or Bun.
- NuGet restore is configured with a single explicit source, the public .NET 9 Azure DevOps feed. No Node package manager is involved in restore or publish.

## Portable package verification

The existing `naps2-portable-win64.zip`:

- Passed `unzip -t` archive-integrity verification.
- Contains the expected `App/NAPS2.exe`, `App/NAPS2.Worker.exe`, and `Data/` layout.
- Contains no suspicious npm/Node paths or names.
- Had no advisory indicator strings found while scanning its `.dll` and `.exe` contents.

A new portable package was built in a separate temporary staging directory from the audited revision. It also passed archive-integrity verification and showed no advisory indicator paths or strings.

The existing ZIP contains 16 additional files under `App/runtimes/` (`ggml`, `llama`, and `llava_shared` CPU runtime DLLs). These are the expected optional local-AI runtime files used by the project's LLamaSharp dependency. The new staging rebuild did not copy those optional runtime files, so it has a different file inventory. This is a packaging difference, not an npm-attack indicator.

The existing ZIP's core binary hashes differ from the staging rebuild because it was created before the current source revision. It should be replaced with a freshly rebuilt portable ZIP before the current source revision is distributed.

## Verification commands used

- File-name and text scans over tracked source, checked-in automation, and generated output (excluding `.git`).
- .NET/NuGet input and package-reference inventory.
- Inspection of `.github/workflows/dotnet.yml`, `scripts/post-merge.sh`, `NuGet.Config`, and build configuration.
- `unzip -t` and file-list inspection of the existing and freshly rebuilt ZIPs.
- String scans of all `.dll` and `.exe` files in both ZIPs for the supplied attack indicators.

## Conclusion and limitations

The audited project and inspected portable package show no evidence of the reported npm supply-chain attack. This conclusion applies only to this repository, its workspace, and the scanned package artifacts. It cannot establish the security state of developer devices, external build machines, third-party services, or credentials; those areas were intentionally excluded from this audit.