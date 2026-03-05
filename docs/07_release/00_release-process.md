# Release Process

## Vorbereitung
- Aenderungen in `developer` konsolidieren.
- CI (`.github/workflows/ci.yml`) muss gruen sein.
- Release-Notes unter `docs/releases/<programm>/` aktualisieren.

## Tag und Installer-Release
- Tag erstellen und pushen, z. B. `0.0.73` (optional `v0.0.73`).
- Workflow `.github/workflows/release-installer.yml` baut:
  - `PCWaechter_offline_installer_<versionCode>.exe`
  - `PCWaechter_live_installer.exe`
  - `installer-manifest.json`
- Optionaler Dry-Run fuer `versionCode`:
  - `.github/workflows/release-versioncode-check.yml`

## Server Deploy
- Workflow `.github/workflows/server-deploy.yml` manuell starten.
- Eingaben:
  - `ref` (Standard `developer`)
  - `compose_path` (Standard `/opt/pcwaechter/server/api/infra/compose`)
  - `build_images` (`true/false`)
- Der Workflow laeuft auf dem Self-Hosted Runner und synchronisiert `server/` nach `/opt/pcwaechter/server/` sowie `server/home/` nach `/opt/pcwaechter/server/home/`.
- Danach wird `docker compose up -d` im Compose-Verzeichnis ausgefuehrt.

## Benoetigte GitHub Secrets (Remote Deploy)
- `DEPLOY_SUDO_PASSWORD` (optional, falls `sudo` Passwort braucht)

## Server-Versionierung
- Nicht an Client-Version koppeln.
- In Compose pro Service ueber Image-Tags steuern (`API_IMAGE`, `CONSOLE_IMAGE`, `HOME_IMAGE`).
- Fuer Server-Programme nur fortlaufende Zahlen verwenden (z. B. `17`, `18`, `19`).

