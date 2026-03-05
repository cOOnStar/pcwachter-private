# 01 — GAP Matrix (Plan3)

Legende:
- **IST**: Zustand im Repo nach plan2 (Commit laut deiner Rückmeldung).
- **Delta**: Was fehlt bis “Support + Zammad korrekt”.
- **Nachweis**: Wo im Code/Setup relevant (Pfad); wenn nicht verifizierbar: `unknown`.
- **Priorität**: P0 (Blocker), P1 (wichtig), P2 (optional/backlog).

| Zielbild-Item | IST | Delta | Nachweis | Priorität |
|---|---|---|---|---|
| Support Self-Service End-to-End (Home User → API → Zammad) | API hat Support Router, Console UI hat Support Pages | **Zammad produktiv konfigurieren** (Base URL, Token, Group-ID, optional Org-ID, Netzwerk/Proxy). Home-UI: **unknown**, je nach Existenz | `server/api/app/routers/support.py`, `server/api/app/settings.py`, `server/console/.../SupportPage.tsx` | P0 |
| Support: klare Fehlermeldung/Hint statt generischem ErrorBanner | UI zeigt ErrorBanner (graceful) | Admin-Hintbox + Nicht-Admin neutrale Meldung + “Konfig prüfen” | `server/console/src/app/pages/SupportPage.tsx` | P1 |
| Support: Admin Diag UI integriert | diag endpoint existiert (roles/user) | Button + Modal/Drawer im Support UI | `api-service.ts` hat `diagZammadRoles()`; UI fehlt | P1 |
| Support: Webhook optional (Zammad → API) | Endpoint existiert (oder geplant) | Zammad Trigger/Webhook einrichten + Shared Secret setzen | `server/api/app/routers/support.py` (webhook), `settings` | P1 |
| Zammad Healthcheck + Compose Profile | unknown | compose ergänzen (optional) + healthcheck + volumes | `server/infra/compose/docker-compose.yml` | P0 |
| Nginx Proxy Manager Host/Headers für support.* | external | Proxy Host anlegen, Websocket/headers, TLS, upload size | NPM UI (external) | P0 |
| Keycloak Token enthält `email` Claim | benötigt für Support | Prüfen/Mapper falls fehlt | Keycloak UI (external), API logs | P0 |
| P2: UpdatesPage (Manifest Viewer) | nicht implementiert | optional page + calls | Console | P2 |
| P2: ClientConfig UI | blocked | Spec fehlt + API/DB evtl. vorhanden | **unknown**: Spec + endpoints | P2 |
| P2: KB CRUD (Console) | read-only | endpoints + role checks + UI | API/Console | P2 |
| P2: Rules UI | blocked | Spec fehlt (`docs/rules/spec.md`) | missing | P2 |
