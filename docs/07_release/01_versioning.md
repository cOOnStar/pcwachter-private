# Versionierung

## Prinzip
- Client-Release und Server-Releases sind getrennt.
- Client-Release nutzt Tags wie `0.0.73` (optional auch `v0.0.73`).
- Server nutzt pro Service eigene, fortlaufende Nummern.

## Server-Image-Tags
- `API_IMAGE`
- `CONSOLE_IMAGE`
- `HOME_IMAGE`

Beispiel:
- `API_IMAGE=ghcr.io/coonstar/pcwaechter-api:17`
- `CONSOLE_IMAGE=ghcr.io/coonstar/pcwaechter-console:9`
- `HOME_IMAGE=ghcr.io/coonstar/pcwaechter-home:4`

## Namensregeln fuer Release-Notes
- Client-Programme:
  - `gui`, `updater`, `service` -> `vX.Y.Z.md` (z. B. `v0.0.73.md`)
- Server-Programme:
  - `api`, `console`, `webseite` -> fortlaufende Zahl mit `v` (z. B. `v1.md`, `v2.md`, `v3.md`)

## Installer-Version-Code
- Der Windows-Installer nutzt zusaetzlich `versionCode`.
- Ableitung: Punkte aus `MAJOR.MINOR.PATCH` entfernen (`MAJORMINORPATCH`) und auf mindestens 4 Stellen links mit `0` auffuellen.

Beispiele:
- `0.0.46` -> `0046`
- `1.2.7` -> `0127`
- `2.4.123` -> `24123`

## GitHub Workflows
- CI: `.github/workflows/ci.yml`
- Installer Release: `.github/workflows/release-installer.yml`
- VersionCode Dry-Run: `.github/workflows/release-versioncode-check.yml`
- Remote Deploy: `.github/workflows/server-deploy.yml`
