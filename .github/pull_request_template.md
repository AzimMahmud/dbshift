## Description

<!-- Briefly describe the change and why it's needed. -->

Fixes #(issue)

## Type of change

- [ ] Bug fix
- [ ] New feature
- [ ] Documentation update
- [ ] CI / infrastructure
- [ ] Refactoring (no functional change)

## Checklist

- [ ] Code builds with zero warnings (`dotnet build`)
- [ ] All tests pass (`dotnet test`)
- [ ] New tests added for any new behaviour
- [ ] `CHANGELOG.md` updated under "Unreleased"
- [ ] README or `docs/USAGE.md` updated if behaviour changed
- [ ] PR title follows [Conventional Commits](https://www.conventionalcommits.org/)

## How to test

```bash
# Commands to verify your change
dbshift validate
dbshift plan
...
```
