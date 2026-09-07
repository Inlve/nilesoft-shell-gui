# Security policy

## Supported versions

Security fixes are provided for the latest released minor version.

| Version | Supported |
| --- | --- |
| 0.2.x | Yes |
| Earlier | No |

## Reporting a vulnerability

Please use GitHub's **Report a vulnerability** private security advisory flow.
Do not open a public issue for vulnerabilities involving arbitrary command
execution, privilege escalation, unsafe configuration replacement, path
traversal, or backup disclosure.

Include affected versions, reproduction steps, impact, and any suggested fix.
Maintainers should acknowledge a report within seven days and publish status
updates until it is resolved or declined.

Shell Studio edits configuration that can launch commands with elevated rights.
Treat NSS files from third parties as executable code and inspect them before
importing.
