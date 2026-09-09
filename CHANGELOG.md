# Changelog

All notable changes to Shell Studio are documented here. The project follows
[Semantic Versioning](https://semver.org/) and this file follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Planned

- Structured builders for `modify` and `remove` rules.
- Drag-and-drop ordering and nested menu creation.
- Shell log diagnostics linked to source lines.

## [0.2.1] - 2026-09-08

### Fixed

- Display NSS tree node content correctly in the WinUI TreeView.
- Compare editor text to the saved baseline, preserving line endings and clearing
  the dirty indicator when edits are undone.
- Validate CR, LF, and CRLF input consistently and navigate to the reported line.
- Update the appearance preview when style, density, item radius, or shadow changes.
- Preserve annotated release tags during CI checkout and support failed-release recovery.

## [0.2.0] - 2026-09-07

### Changed

- Migrated the desktop interface from WPF to WinUI 3 and Windows App SDK 2.4.
- Split Shell configuration logic into a separately testable Core project.
- Adopted unpackaged, self-contained, single-file x64 distribution.

### Added

- Community contribution, conduct, security, support, and governance policies.
- Pull request validation, conventional title checks, dependency updates, and
  semantic-tag release automation.

## [0.1.0] - 2026-09-07

### Added

- Initial WPF prototype with NSS discovery, parsing, editing, validation,
  backups, menu item creation, theme generation, and Shell reload support.

[Unreleased]: https://github.com/Inlve/nilesoft-shell-gui/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/Inlve/nilesoft-shell-gui/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/Inlve/nilesoft-shell-gui/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Inlve/nilesoft-shell-gui/releases/tag/v0.1.0
