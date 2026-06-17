# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| 1.x     | ✅ |

## Reporting a vulnerability

We take security seriously. If you discover a security vulnerability in DbShift,
please **do not** open a public issue. Instead, report it privately:

- Open a [draft security advisory](https://github.com/your-org/dbshift/security/advisories/new)
- Or email the maintainers directly (check the commit history for contacts)

We will acknowledge receipt within 48 hours and provide a timeline for a fix.
Vulnerabilities will be disclosed after a fix is released.

## Scope

- The `dbshift` binary and its source code
- The install scripts (`install.sh`, `install.ps1`)
- The build/publish scripts

## Out of scope

- User-authored SQL migration scripts (we validate syntax but not intent)
- Third-party NuGet packages (report to their respective maintainers)
- Database servers and infrastructure
