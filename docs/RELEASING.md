# Release process

Shell Studio follows Semantic Versioning and publishes immutable releases from
signed annotated Git tags.

## Choose the version

- Patch: backward-compatible bug fix.
- Minor: backward-compatible feature.
- Major: incompatible behavior, storage, or configuration contract.

Before release, update `VersionPrefix` in `Directory.Build.props`, move relevant
entries from `Unreleased` into a dated section in `CHANGELOG.md`, and merge those
changes into `main`.

## Validate and tag

With a clean `main` checkout and Git signing configured:

```powershell
.\scripts\release.ps1 -Version 0.2.0
git push origin v0.2.0
```

The script verifies the branch, clean working tree, declared version, changelog,
build, smoke tests, and signed tag locally.

## Automated release

The tag workflow independently:

1. Rejects non-semantic, lightweight, or version-mismatched tags.
2. Restores, builds, and runs Core smoke tests on a clean runner.
3. Publishes the WinUI 3 app as an unpackaged, self-contained x64 single EXE.
4. Generates a SHA-256 checksum and GitHub build provenance attestation.
5. Creates a GitHub Release with automatically generated release notes.

Published tags are immutable. If a release is wrong, fix it in a new patch
release; never move or overwrite the existing tag.
