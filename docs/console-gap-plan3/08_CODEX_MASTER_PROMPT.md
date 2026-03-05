# 08 — CODEX / CLAUDE Master Prompt (Plan3)

> Ziel: Restarbeiten für “Support vollständig” + Console UX + Setup Docs in **einem** Durchlauf.

```text
Du bist Senior Staff Engineer (Backend/DevOps/Frontend) für PCWächter.
Arbeite NUR mit dem Repo. Nichts erfinden: unknown + fehlende Quelle nennen.

Ziel (Plan3):
1) Console Support UX verbessern:
   - Bei 503 support_not_configured: Admin-Hintbox mit benötigten ENV Vars.
   - Bei 502 zammad_unreachable...: Admin-Hintbox "Zammad nicht erreichbar" + Diag CTA.
   - Nicht-Admin: neutrale Meldung ohne interne Details.
2) Console Support Diag UI:
   - Button "Diag: Zammad Roles" (nur Admin)
   - Optional: "Diag: Zammad User" (admin, email input)
   - Nutze bestehende api-service.ts Funktionen: diagZammadRoles(), diagZammadUser() falls vorhanden; sonst hinzufügen.
3) Dokumentation:
   - Lege docs/console-gap-plan3/* Dateien an (Scope/GAP/Runbook/Rollback/DoD/Zammad Guide/ENV Matrix).
   - Aktualisiere docs/audit2 oder docs/support/smoke-tests.md nur wenn nötig (keine Regressions).
4) (Optional, wenn vorhanden) Home Support Seite:
   - Wenn Home bereits Support hat: nichts.
   - Wenn nicht: Implementiere minimal read/create/reply/upload gegen /api/v1/support/*.
   - Wenn unklar: markiere unknown und lege nur Doku + TODO an.

Regeln:
- Keine DB Migrationen.
- Keine Änderung an Support Backend Semantik (nur UI/Docs).
- npm run build (server/console) muss grün.

Output (verbindlich):
1) Tracking Tabelle: Item -> PASS/FAIL/PARTIAL -> Nachweis (Datei+Zeilen) -> offene Punkte.
2) Unified diff Patch.
3) Commands: npm build + 2 smoke calls (one failing scenario + one success scenario).
4) Rückmelde-Template (siehe unten) 1:1 ausgeben.

Rückmelde-Template (1:1 ausgeben):
RÜCKMELDUNG (console-gap-plan3)
Repo:
Branch:
Commit:

Support UI Hintbox
Status (PASS/FAIL):
Nachweis:
Notes:

Support Diag UI
Status (PASS/FAIL):
Nachweis:
Notes:

Docs Pack
Status (PASS/FAIL):
Files:
Notes:

Home Support (optional)
Status (PASS/FAIL/SKIP):
Notes:

git diff --stat:
<paste>

Tests
npm run build: PASS/FAIL
Errors (falls FAIL): <cmd + error>
```
