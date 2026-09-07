## Summary

Describe the user-visible outcome and why the change is needed.

## Validation

- [ ] `dotnet build ShellStudio.sln -c Release` passes.
- [ ] Smoke tests pass.
- [ ] Existing NSS files are preserved unless the change explicitly requires otherwise.
- [ ] User-visible changes are listed under `Unreleased` in `CHANGELOG.md`.
- [ ] Screenshots are included for visual changes.

## Safety

- [ ] No live Shell configuration is modified by tests.
- [ ] File writes retain validation, backup, and atomic replacement.
- [ ] Explorer is not restarted without user confirmation.
