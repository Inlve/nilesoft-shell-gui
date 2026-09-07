# Contributing to Shell Studio

Thank you for helping make Nilesoft Shell easier to configure. Small, focused
pull requests are easiest to review.

## Development setup

- Windows 10 19041 or newer.
- .NET SDK 9.0.308 or a compatible 9.0 patch release.
- Visual Studio with Windows application development components is recommended.
- Nilesoft Shell is optional for parser tests, but required for end-to-end checks.

```powershell
dotnet restore ShellStudio.sln
dotnet build ShellStudio.sln -c Release
dotnet run --project tests/ShellManager.Tests
```

## Workflow

1. Open an issue for substantial behavior or UI changes.
2. Create a branch such as `feat/menu-drag-drop` or `fix/import-parser`.
3. Keep Core independent from WinUI so it remains testable without a UI process.
4. Add a smoke test for parser and configuration behavior.
5. Update `CHANGELOG.md` under `Unreleased` for user-visible changes.
6. Open a pull request using the repository template.

## Commit and pull request titles

Use Conventional Commits:

```text
feat: add nested menu builder
fix(parser): preserve braces inside strings
docs: explain unpackaged deployment
```

Allowed types are `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`,
`build`, `ci`, `chore`, and `revert`. Add `!` or a `BREAKING CHANGE:` footer for
an incompatible change.

## Versioning

Shell Studio follows Semantic Versioning:

- `MAJOR`: incompatible behavior or configuration format change.
- `MINOR`: backward-compatible feature.
- `PATCH`: backward-compatible fix.

The canonical version is `VersionPrefix` in `Directory.Build.props`. A release
tag must be the same version prefixed with `v`, for example `v0.2.0`.

## Safety requirements

- Never rewrite or reformat an existing NSS file unless the user explicitly asks.
- Always validate and back up before replacing configuration.
- Never restart Explorer without explicit confirmation.
- Treat imported paths and Shell expressions as untrusted input.
- Tests must not modify the live Nilesoft Shell installation.
