# One‑Shot Umsetzungsplan (audit2 → Zielbild v6.3)

**Prinzip:** in einem koordinierten Durchlauf möglichst viele Deltas schließen, aber mit sauberem Rollback.

## Reihenfolge (mit Abhängigkeiten)

### 0) Preconditions (muss zuerst stimmen)
- Repo ist ein Git-Working-Tree (`.git` vorhanden) und du bist im Repo‑Root.
- `python -m py_compile` muss laufen (mindestens für API Router).
- Für API Smoke-Tests: lokale .env (oder Compose) mit `ZAMMAD_BASE_URL` etc. (sofern Support getestet wird).

### 1) P0 — Agent Register Legacy default OFF (Backend)
**Abhängigkeit:** keine DB migration  
**Ergebnis:** fail-fast wenn Bootstrap nicht konfiguriert und Legacy nicht explizit erlaubt.

### 2) P0 — Greenfield DB Init standardisieren
**Abhängigkeit:** kann Code + Compose berühren.  
**Ergebnis:** `db-init` Script (oder one-shot service) + Compose‑Option, die bootstrap.sql idempotent ausführt und danach `alembic upgrade head`.

### 3) P0 — Release/Update Variante A Workflow produktionsfähig machen
**Abhängigkeit:** build pipeline; evtl. Windows runner.  
**Ergebnis:** stabile Assets + installer-manifest.json (sha256) + Release Upload zu `pcwaechter-public-release`.

### 4) P1 — Support Reply + Attachments
**Abhängigkeit:** None (rein Router)  
**Ergebnis:** Reply via Zammad `/api/v1/ticket_articles` + Upload endpoint (multipart).

### 5) P1 — Notifications persistent
**Abhängigkeit:** DB migration + router + models + console/home calls ggf.  
**Ergebnis:** persistente Tabelle + 3 Endpoints (list, read, read-all).

### 6) P2 cleanup
client_config / KB / downloads / ops docs nachziehen – nur wenn in audit2 offen.

## Output nach dem One‑Shot
- `docs/audit2/IST_Matrix.md` aktualisiert (IST→PASS, Delta→0) oder als `audit3` neu generiert
- `docs/audits_fix/REPLY_TEMPLATE.md` ausgefüllt
