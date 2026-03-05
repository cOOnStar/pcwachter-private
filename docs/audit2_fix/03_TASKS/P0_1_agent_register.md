# Task P0-1 — Agent Register: Legacy default OFF + fail-fast

## Ziel
- `ALLOW_LEGACY_API_KEY_REGISTER` ist standardmäßig **false**.
- Wenn **AGENT_BOOTSTRAP_KEY fehlt** und Legacy nicht aktiv, dann **503 agent_bootstrap_not_configured** (actionable).
- Legacy-Pfad bleibt optional (für Migration) über Env Toggle.

## Umsetzung (Codex soll das im Repo machen)
1. `server/api/app/settings.py`
   - Setze Default: `ALLOW_LEGACY_API_KEY_REGISTER: bool = False`
   - Stelle sicher: `AGENT_BOOTSTRAP_KEY` ist required für register, sofern Legacy nicht explizit true ist.

2. `server/api/app/security.py` (oder wo `require_agent_register()` lebt)
   - In `require_agent_register()`:
     - Wenn `settings.AGENT_BOOTSTRAP_KEY` leer/whitespace:
       - Wenn `settings.ALLOW_LEGACY_API_KEY_REGISTER` true → legacy erlauben (kompatibel)
       - Sonst → raise HTTPException(503, "agent_bootstrap_not_configured")
     - Wenn Bootstrap gesetzt:
       - Validierung nur gegen Bootstrap-Key (kein API-Key).

3. `server/api/.env.example` oder docs:
   - Ergänze Kommentar, wie man Legacy **temporär** einschaltet.
   - Beispiel:
     - `ALLOW_LEGACY_API_KEY_REGISTER=true` (nur Migration)
     - danach wieder false.

## Akzeptanz / Smoke
- Ohne `AGENT_BOOTSTRAP_KEY` und ohne Legacy:
  - `POST /api/v1/agent/register` → **503 agent_bootstrap_not_configured**
- Mit `AGENT_BOOTSTRAP_KEY`:
  - falscher key → 401
  - richtiger key → 200
- Mit Legacy toggle true:
  - alter Key Pfad funktioniert (falls im Code noch vorhanden)

## Rückmeldung (bitte nach Umsetzung)
- `git diff` Auszug (settings.py + security.py)
- Curl outputs (503/401/200)
