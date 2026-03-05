# Risikoanalyse + Rollback-Plan (v6.3 Update/Support/DB)

## Hauptrisiken

1) **DB Migrationen** (Spalten/Tabellen)
- Risiko: Migration schlägt fehl oder widerspricht Runtime-Schema.
- Mitigation:  
  - `alembic current/heads` vor/nachher dokumentieren  
  - Migration idempotent / additive halten (nur ADD COLUMN)  
- Nachweis Alembic Head: `audit_05.03.2026/06-migrations.md:L27-L33`

2) **Agent Register Auth Änderung (API-Key entfernen)**
- Risiko: bestehende Agents können sich nicht (neu) registrieren.
- Mitigation:  
  - Feature flag / config toggle (serverseitig) für Übergangszeit (z. B. `ALLOW_LEGACY_API_KEY_REGISTER=true`)  
  - Rollout in Stufen über `feature_overrides` (Kill Switch)  
- Nachweis Kill-Switch/Rollout: `audit_05.03.2026/07-feature-coverage.md:L15`

3) **Updater/Installer Supply-Chain**
- Risiko: Live Installer lädt falsches Asset / Hash mismatch → Install bricht.
- Mitigation:
  - Manifest + sha256 Pflicht
  - TLS only + optional Code-Signature check
  - Staged rollout via update channel (stable/beta/internal)

4) **Support/Zammad Integration**
- Risiko: Token/Secrets falsch → Tickets gehen nicht durch.
- Mitigation:
  - Erst „read-only“ Endpoints (GET tickets) live schalten
  - Webhook endpoint getrennt deployen (rate limits + shared secret)

## Rollback

### A) Server Rollback (Docker)
1. API auf vorheriges Image zurückrollen:
```bash
docker compose pull api
docker compose up -d api
```
2. Feature killen (ohne Rollback):
- `feature_overrides.enabled=false` für betroffene Feature Keys (Console `/features`)  
  Nachweis Feature UI existiert: `audit_05.03.2026/01-page-matrix.md` (Route `/features`)

### B) DB Rollback
- Additive Spalten können i. d. R. bleiben (harmlos), auch wenn Code zurückgerollt wird.
- Wenn unbedingt nötig:
  - Alembic downgrade (nur wenn Migration reversible implementiert ist)
  - oder manuell `ALTER TABLE ... DROP COLUMN ...` (letztes Mittel)

### C) Client Rollback
- GitHub Releases: „Previous“ Release Assets beibehalten.
- Live Installer: Fallback auf „last known good“ Version (manifest enthält previous).

