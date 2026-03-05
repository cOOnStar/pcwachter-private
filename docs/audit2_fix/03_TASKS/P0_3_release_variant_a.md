# Task P0-3 — Update/Release Variante A: Offline Build Placeholder entfernen

## Ziel (Variante A)
- Assets mit **stabilen Namen** (Underscores) werden gebaut & als GitHub Release Assets hochgeladen:
  - `PCWaechter_offline_installer.exe`
  - `PCWaechter_live_installer.exe`
  - `installer-manifest.json` (sha256 + urls)
- Home `/download` zeigt stabile Links und kann optional Manifest lesen.

## Umsetzung (Codex)
1. `docs/audit2/UPDATE_RELEASE_VARIANT_A.md` als Referenz nehmen.
2. Prüfe bestehende Workflow(s):
   - `.github/workflows/publish_release.yml` (private repo)
   - ggf. Template in `docs/release/...`
3. Workflow konkretisieren:
   - Offlinesetup build step (kein Placeholder mehr)
   - Live bootstrapper build step
   - Rename/Copy outputs auf stabile Asset-Namen
   - SHA-256 berechnen (für beide)
   - Manifest JSON generieren
   - Release upload zu `pcwaechter-public-release` (token/permission: **unknown**, muss in Secrets)
4. `scripts/publish_release.ps1` ggf. angleichen (lokal gleiche Asset-Namen).

## Akzeptanz
- CI run produziert Release Assets mit exakten Namen.
- Manifest referenziert die exakten Asset‑URLs + sha256.
- (Optional) Live installer lädt offline installer, verifiziert sha256.

## unknown / Team-Input
- Wie genau Offline‑Installer gebaut wird (NSIS/Inno/msix) → muss aus Repo-Pfaden ableitbar sein.
- Token/Permission für Cross-Repo Release Upload.

## Rückmeldung
- Workflow link / run output
- Release Asset Liste
- `installer-manifest.json` Beispiel
