Du bist Senior Staff Engineer (Frontend+Backend) für PCWächter.
Arbeite NUR mit dem vorhandenen Repo-Inhalt.
Ziel: Implementiere **console-gap-plan1** gemäß `docs/console-gap-plan1/02_ONE_SHOT_PLAN.md`.

Regeln
- Nichts erfinden. Wenn etwas fehlt/unklar ist: markiere `unknown` und nenne fehlende Quelle.
- Halte Änderungen additiv, idempotent.
- Verwende bestehende Patterns/Components/Styles im Console-Code.
- Kein Code Signing/Authenticode.
- Support muss Self‑Service bleiben: normale User sehen nur eigene Tickets; Admin sieht `all=true`.

Deliverables (verbindlich)
1) Tracking Tabelle: Item -> PASS/FAIL/PARTIAL -> Nachweis (Datei) -> offene Punkte
2) Implementierte Änderungen als Code im Repo
3) Commands (Build/Tests) + erwartete Outputs
4) DoD Checkliste (kurz, prüfbar)
5) Gib mir am Ende exakt das Rückmelde-Template aus `docs/console-gap-plan1/04_RUECKMELDUNG_TEMPLATE.md` zum Ausfüllen.

Umsetzungsschritte (strikt in Reihenfolge)
A) Console Frontend
- Finde `Sidebar.tsx` und ergänze NAV_ITEMS um:
  - /activity (Activity Feed)
  - /knowledge-base (Knowledge Base)
  - /support (Support)
  - optional /updates (Updates/Manifest Viewer) [P2]
- Finde Router/Routes Datei und ergänze Routen entsprechend.
- Implementiere neue Pages:
  - ActivityFeedPage.tsx: list items (timestamp, type, message)
  - KnowledgeBasePage.tsx: list KB items (title, category, updated_at, tags)
  - SupportPage.tsx + SupportTicketDetailPage.tsx:
    - list tickets, filter admin all=true toggle sichtbar nur für Admin
    - detail view + reply + upload
- Erweitere `api-service.ts` um neue Calls:
  - getActivityFeed, getKnowledgeBase
  - support: list/get/reply/upload + diag roles (admin)
  - setDeviceUpdateChannel

B) Backend (klein, additiv)
- Ergänze in `server/api/app/routers/console.py`:
  - PATCH `/api/v1/console/ui/devices/{device_id}/update-channel`
  - Auth: require_console_owner
  - Valid channels: stable|beta|internal
  - Persist: update devices.update_channel + updated_at.
- Ergänze OpenAPI schema (Pydantic model) für Request/Response.

C) Smoke Checks
- `cd server/console && npm run build`
- Minimal API curl checks für die neuen Endpoints.

Ausgabeformat
- Tracking Tabelle als Markdown
- Danach "Commands" Block
- Danach DoD Checklist
- Dann Rückmelde-Template 1:1
