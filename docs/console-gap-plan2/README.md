# PCWächter – Console GAP Plan 2 (Backend Endpoints)
Stand: 2026-03-05 (Europe/Berlin)

## Zweck
Schließt die **letzten P1-Lücken** aus *console-gap-plan1* auf der API-Seite:

- `GET /api/v1/console/ui/activity-feed`
- `GET /api/v1/console/ui/knowledge-base`

Damit zeigen die neuen Console-Seiten **keine ErrorBanner mehr** (stattdessen echte Daten bzw. leere Listen, je nach DB-Inhalt).

## Scope / Regeln
- Nur Änderungen im Monorepo `pcwachter-private`.
- Additiv, migrations-sicher.
- Keine Code-Signing / Authenticode Themen.

## Enthaltene Dateien
- `01_GAP_MATRIX.md`
- `02_ONE_SHOT_PLAN.md`
- `03_CODEX_PROMPT.md`  (1:1 in Codex/Claude einfügen)
- `04_RUECKMELDUNG_TEMPLATE.md`
