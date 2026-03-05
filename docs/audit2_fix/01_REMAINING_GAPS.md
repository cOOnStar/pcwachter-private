# Remaining Gaps aus audit2 (Zielbild v6.3)

> **Regel:** Nichts erfinden. Wenn etwas im Repo nicht belegbar ist: `unknown` + fehlende Quelle nennen.

## P0 (Blocker / muss für v6.3 „serverseitig erfüllt“)
1. **Agent Register Legacy-Key default OFF**  
   - Problem (audit2): Legacy path ist aktuell standardmäßig aktiv oder nicht fail-fast, wodurch Zielbild „kein statischer API-Key“ verwässert.  
   - Ziel: `ALLOW_LEGACY_API_KEY_REGISTER` default **false**, und wenn weder Bootstrap-Key gesetzt noch Legacy explizit erlaubt, dann **503** mit actionable message.
2. **Greenfield DB Bootstrap automatisieren / standardisieren**  
   - Problem (audit2): Greenfield ist nicht „Alembic-only“ reproduzierbar; es gibt bootstrap SQL, aber der Ablauf ist nicht in Compose/Entry consistent.  
   - Ziel: Ein *idempotenter* DB-Init‑Pfad (bootstrap.sql + alembic upgrade) der in dev/stage reproducible ist.
3. **Update/Release Variante A: Offline-Build Placeholder entfernen**  
   - Problem (audit2): Workflow/Docs vorhanden, aber Offline‑Installer Build in CI ist Placeholder bzw. Name-Mismatch Risiko.  
   - Ziel: Workflow baut echte Artefakte mit *stabilen* Asset-Namen + Manifest (sha256) + Release Upload.

## P1 (wichtig, aber kein Blocker wenn man bewusst „partial“ akzeptiert)
4. **Support: Reply + Attachments Endpoints**  
   - Problem (audit2): Self‑Service Support ist da, aber reply/upload fehlen bzw. sind unvollständig.  
   - Ziel:  
     - `POST /api/v1/support/tickets/{id}/reply` (text/plain + optional attachments)  
     - `POST /api/v1/support/attachments` (multipart → return attachment-json for Zammad)
5. **Notifications persistent (Read-State + Speicherung)**  
   - Problem (audit2): Notifications sind teils transient / Read-State nicht persistiert.  
   - Ziel: DB‑basiertes Notification Modell + Endpoints (read, read-all) mit idempotentem Verhalten.

## P2 (nice-to-have / Architektur sauber ziehen)
6. **client_config remote** (Admin edit + client poll) – falls audit2 noch „partial/unknown“
7. **Downloads / KB persistence** – wenn derzeit nur placeholder/static
8. **Keycloak/Realm Ops**: realm export backup, client cleanup – ops task
9. **Rules Engine** – nur wenn Spezifikation im Repo existiert, sonst `unknown`

➡️ Details pro Punkt siehe `03_TASKS/`.
