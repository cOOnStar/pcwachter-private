# GitHub Releases – Variante A (latest/download)

## 1) Asset-Namen pro Release (MUSS stabil sein)
- installer-manifest.json
- PCWaechter_offline_installer.exe
- PCWaechter_live_installer.exe

Wenn Namen variieren, brechen die URLs:
`.../releases/latest/download/<name>`

## 2) Manuell (Phase 1 schnell)
1. Create Release (Tag z.B. v0.0.76)
2. Upload 3 Assets (mit obigen Namen)
3. Teste URLs via curl -I

## 3) Automatisch (Phase 1.5)
Du brauchst eine Pipeline, die:
- Offline & Live Installer baut
- SHA256 berechnet
- Manifest erzeugt
- Release erstellt + Assets hochlädt

Dazu gibt es 2 Varianten:
A) Workflow im Monorepo, der ins Release-Repo publisht (PAT nötig)
B) Lokales Script + gh cli

Siehe:
- `monorepo_publish_release.yml`
- `publish_release.ps1`
