# Remaining GAP Matrix (Zielbild v6.3 vollständig)

| Item | Status aus Claude | Was fehlt genau | Priorität |
|---|---|---|---|
| GitHub Release Repo (pcwaechter-public-release) automatisiert befüllen | PARTIAL | Workflow in **richtigen Repo** übernehmen + Build/Upload Steps füllen + Secrets/PAT einrichten | P0 |
| Live Installer (Binary + Code) | unknown/PARTIAL | Implementieren: Manifest laden -> Offline laden -> SHA256 prüfen -> Start Installer; optional Signaturprüfung | P0 |
| Offline Installer Asset-Namen stabil | unknown | Sicherstellen: Asset-Namen pro Release identisch, sonst brechen latest/download Links | P0 |
| Zammad Mapping (Keycloak User -> Zammad User) | TODO | Mapping per E-Mail; create-if-missing; richtige Org/Group/Customer Zuordnung | P1 |
| Update Event Reporting (/v1/updates/report) | optional | Für Rollout/Monitoring später sinnvoll (nicht zwingend für Phase 1) | P2 |
| Code Signing (Publisher nicht unbekannt) | unknown | Authenticode Zertifikat + Signierung von live/offline; optional Manifest signieren | P1 |
