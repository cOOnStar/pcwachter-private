# Update / Release – Variante A (GitHub Releases)

## Zielbildquelle im Repo
- Variante A ist im Repo explizit als „GitHub Releases / latest/download“ beschrieben.
- Nachweis: `docs/audit_fix_05.03.2026/02_ONE_SHOT_PLAN.md:1-4`, `158-167`.

## IST im privaten Repo (dieses Repository)

### 1) Workflow für Veröffentlichung in `pcwaechter-public-release`
- Workflow vorhanden: `.github/workflows/publish_release.yml`.
- Zielrepo ist extern gesetzt: `cOOnStar/pcwaechter-public-release`.
- Release erfolgt via `gh release create` in diesem externen Repo.
- Nachweis: `.github/workflows/publish_release.yml:19`, `89-105`.

### 2) Stabile Asset-Namen (Underscore-Schema)
- Im Workflow als Pflichtnamen definiert:
  - `installer-manifest.json`
  - `PCWaechter_offline_installer.exe`
  - `PCWaechter_live_installer.exe`
- Nachweis: `.github/workflows/publish_release.yml:4-7`, `20-21`, `103-105`.

### 3) Manifest-Generierung inkl. SHA-256
- Workflow berechnet SHA-256 für Offline- und Live-Installer und schreibt beides ins Manifest.
- Nachweis: `.github/workflows/publish_release.yml:63-84`.
- Lokales Pendant vorhanden: `scripts/publish_release.ps1` (gleiche Asset-Namen, SHA-256, Release-Upload).
- Nachweis: `scripts/publish_release.ps1:27-30`, `55-85`, `95-101`.

### 4) Home Download Page nutzt Variante-A URL-Schema
- Default-Base: `.../releases/latest/download`.
- Download-Links für Offline/Live sind direkt auf stabile Asset-Namen aufgebaut.
- Manifest wird clientseitig geladen und SHA-256 optional angezeigt/kopiert.
- Nachweis: `server/home/src/app/download/page.tsx:5-11`, `32-35`, `93-99`, `139-145`, `99-120`, `145-166`.

### 5) Live-Installer Verhalten (Manifest + Download + Hash Verify)
- Bootstrapper lädt standardmäßig `installer-manifest.json` von `latest/download`.
- Lädt Installer-URL aus Manifest und verifiziert SHA-256 (wenn gesetzt).
- Self-Update des Live-Installers ist ebenfalls über Manifest vorgesehen (inkl. SHA).
- Nachweis: `client/installer/bootstrapper/Program.cs:12`, `44-70`, `210-237`, `253-267`, `517-527`.

## Gaps / Risiken im privaten Repo

| Thema | IST | Delta | Nachweis | Prio |
|---|---|---|---|---|
| Offline-Build im Workflow | Platzhalter prüft nur, ob Datei bereits existiert; echter Build-Step fehlt | Echten Build für Offline-Installer ergänzen (oder Artifact-Download) | `.github/workflows/publish_release.yml:31-44` | P0 |
| Dateinamens-Konsistenz Build↔Release | NSIS erzeugt `PCWaechter_offline_installer_${APP_VERSION_CODE}.exe`, Workflow erwartet `PCWaechter_offline_installer.exe` | Build-Pipeline muss in stabilen Namen kopieren/normalisieren vor Upload | `client/installer/nsis/PCWaechterSetup.nsi:224-226`, `.github/workflows/publish_release.yml:20`, `41-44` | P0 |
| Manifest-Dateien im Repo | Versionierte Manifest-Dateien sind reduziert (`installer.sha256` leer, ohne `bootstrapper/runtime`) | Für Releases nur generiertes Manifest aus Workflow/Script als Release-Asset verwenden; statische Datei nicht als Wahrheit behandeln | `release/installer-manifest.json:1-7`, `client/installer/manifests/installer-manifest.json:1-8`, `.github/workflows/publish_release.yml:67-84` | P1 |
| Namensschema in Zielbild-Doku | Teile der v6.3-Doku nutzen Bindestrich-Namen (`PCWaechter-Offline-Setup.exe`), Code/Workflow nutzen Underscore-Namen | Einheitliches Asset-Namensschema final festlegen und Doku/Code angleichen | `docs/audit_fix_05.03.2026/05_DOD_CHECKLIST.md:9-11`, `docs/audit_fix_05.03.2026/templates/frontend/home_download_page_update.md:6-8`, `.github/workflows/publish_release.yml:4-7` | P1 |

## Was im `public-release` Repo umgesetzt/verifiziert sein muss

| Muss-Zustand (Variante A) | Quelle im privaten Repo | Status |
|---|---|---|
| Releases enthalten `installer-manifest.json`, `PCWaechter_offline_installer.exe`, `PCWaechter_live_installer.exe` | `.github/workflows/publish_release.yml:4-7`, `99-105` | `unknown` (public repo Inhalt nicht im Workspace) |
| `latest/download` URLs für alle drei Assets liefern 200 | `.github/workflows/publish_release.yml:61`, `111-117`; `server/home/src/app/download/page.tsx:7`, `93`, `139` | `unknown` (externe Verifikation nötig) |
| Manifest enthält gültige SHA-256 Werte | `.github/workflows/publish_release.yml:63-84`, `scripts/publish_release.ps1:55-85` | `unknown` (letzter veröffentlichter Release nicht im Repo sichtbar) |

## Unknowns (fehlende Quellen)
- Aktueller Inhalt/Workflow des externen Repos `cOOnStar/pcwaechter-public-release`: `unknown`.
- Fehlende Quelle: Checkout/Dateiinhalte dieses externen Repos sind nicht Teil dieses Workspaces.
- Externe Verifikations-Commands:
  - `gh release list --repo cOOnStar/pcwaechter-public-release`
  - `gh release view --repo cOOnStar/pcwaechter-public-release --json tagName,assets`
  - `curl -I https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download/installer-manifest.json`
