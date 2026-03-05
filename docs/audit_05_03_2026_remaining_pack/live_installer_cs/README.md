# PCWächter Live Installer (C# Skeleton)

Ziel: Win-Executable, die:
1) `installer-manifest.json` von GitHub lädt (Variante A: releases/latest/download)
2) Offline-Installer lädt
3) SHA256 prüft
4) Offline-Installer startet (silent/normal)

## Build
```powershell
dotnet build -c Release
```

## Run (Test)
```powershell
PCWaechter.LiveInstaller.exe --channel stable
```

## Hinweise
- Signaturprüfung (Authenticode) ist optional. Dieses Skeleton macht standardmäßig:
  - SHA256 Validate (Pflicht)
  - Optional: Subject-Allowlist Check als Stub
