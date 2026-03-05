# Installer Bootstrapper (.NET, Legacy)

> Hinweis: Dieser .NET-Bootstrapper ist ein älterer Ansatz und wird aktuell nicht für öffentliche Releases verwendet.
> Aktueller Release-Pfad: `client/installer/nsis/PCWaechterBootstrapper.nsi` (NSIS + INetC).

Dieser Bootstrapper setzt genau den gewünschten Ablauf um:

1. Manifest laden
2. Prüfen, ob es eine neuere Bootstrapper-Version gibt
3. Falls ja: neue Bootstrapper-EXE laden, validieren, neu starten
4. Danach neuestes Installer-Paket laden
5. Installer starten

Der Bootstrapper ist als **kleiner Web-Installer** gedacht (ähnlich Chrome):

- Er enthält **nicht** die komplette App.
- Er lädt nur Metadaten + das aktuelle Setup nach.
- Das eigentliche Produktpaket ist versioniert, z. B. `PCWaechter_offline_installer_0046.exe`.

## Manifest-Strategie

Die Datei `installer-manifest.json` liegt idealerweise im Repo (z. B. `client/installer/manifests/installer-manifest.json`) und wird per RAW-URL geladen.

Beispielstruktur: siehe `installer-manifest.sample.json`.

- `bootstrapper.version`, `bootstrapper.url`, `bootstrapper.sha256` steuern Self-Update.
- `installer.url` kann optional direkt gesetzt werden.
- Wenn `installer.url` leer ist, nutzt der Bootstrapper `github.owner/repo` und lädt `releases/latest`.

## Starten

```powershell
dotnet run --project client/installer/bootstrapper -- --manifest-url "https://raw.githubusercontent.com/<user>/pcwaechter/main/client/installer/manifests/installer-manifest.json"
```

Optionen:

- `--manifest-url <url>`: eigene Manifest-URL
- `--skip-self-update`: intern genutzt nach Self-Update-Neustart

## Sicherheits-Hinweise

- Code-Signing ist aktuell vorübergehend deaktiviert und wird später wieder aktiviert.
- `sha256` im Manifest für Bootstrapper immer setzen.
- Für Installer ebenfalls Hash setzen, auch wenn aktuell ohne Signatur ausgeliefert wird.

## Release-Ablauf (empfohlen)

1. Neue App-Version bauen und das versionierte Asset (z. B. `PCWaechter_offline_installer_0046.exe`) als GitHub Release Asset hochladen.
2. (Optional) neuen Bootstrapper bauen und als separates Asset hochladen.
3. `installer-manifest.json` mit neuer Bootstrapper-Version/URL/Hash aktualisieren.
4. Nutzer starten nur den Bootstrapper; dieser holt automatisch die passende neueste Version.

## Wie geht es jetzt konkret weiter?

Kurzantwort: **Nur Main-Branch veröffentlichen erzeugt noch keine MSI/Setup-Datei.**

Ihr braucht zusätzlich einen Packaging-Schritt, der aus der App ein Installer-Artefakt erzeugt (z. B. `PCWaechter_offline_installer_XXXX.exe` oder `.msi`).

Empfohlene Reihenfolge für `v1.0.0`:

1. App bauen (`dotnet publish`) und daraus Installer bauen (z. B. Inno Setup oder WiX).
2. Bootstrapper bauen (`client/installer/bootstrapper`).
3. GitHub Release Tag `v1.0.0` erstellen.
4. Als Assets hochladen:
	- `PCWaechter_offline_installer_XXXX.exe` (oder `.msi`)
	- `PCWaechter_live_installer.exe`
5. `client/installer/manifests/installer-manifest.json` aktualisieren:
	- `bootstrapper.version` auf neue Version
	- `bootstrapper.url` und `bootstrapper.sha256`
6. Manifest in `main` committen/pushen.

Dann funktioniert der Ablauf so:

- Nutzer startet Bootstrapper
- Bootstrapper prüft Manifest (Self-Update)
- Danach lädt er das neueste Setup aus GitHub Releases und startet die Installation

## Setup EXE bauen (Inno Setup)

Der empfohlene Weg ist der GitHub-Workflow `.github/workflows/release-installer.yml`.

Ergebnis:

- `release/artifacts/PCWaechter_offline_installer_XXXX.exe`

Hinweis:

- Für `.msi` braucht ihr statt Inno Setup z. B. WiX Toolset.
- Für den aktuellen Bootstrapper-MVP reicht ein versioniertes `PCWaechter_offline_installer_XXXX.exe` als Release-Asset.

## Automatisch per GitHub Actions

Workflow: `.github/workflows/release-installer.yml`

- Trigger: Push eines Tags `X.Y.Z` (optional `vX.Y.Z`, z. B. `0.0.73`) oder manuell (`workflow_dispatch`)
- Baut automatisch:
	- `release/artifacts/PCWaechter_offline_installer_XXXX.exe`
	- `release/artifacts/bootstrapper/PCWaechter_live_installer.exe`
	- `release/artifacts/installer-manifest.json` (inkl. SHA256 vom Bootstrapper)
- Lädt diese Dateien direkt als Assets in das passende GitHub Release hoch.

Hinweis zur Größe:

- Der Bootstrapper wird im Release als NativeAOT-EXE gebaut.
- Dadurch ist keine separate .NET Runtime auf Zielsystemen nötig (nutzerfreundlich).
- Typischer Effekt: deutlich kleiner als klassisches self-contained .NET, aber größer als ein sehr kleiner nativer Stub in C++/Rust.

Tag erstellen und pushen:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Für den kompletten Ablauf zu Branches/Tags/Release Notes siehe auch:

- `docs/07_release/00_release-process.md`
