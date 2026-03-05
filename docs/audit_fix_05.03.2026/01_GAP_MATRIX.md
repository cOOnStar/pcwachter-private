# PCWächter – GAP-Matrix (IST Audit 05.03.2026 → Zielbild v6.3)

**Regeln eingehalten:**
- IST-Aussagen basieren ausschließlich auf den Audit-Dateien in `audit_05.03.2026/`.
- Wo Audit keine Aussage zulässt: **unknown** + fehlende Quelle genannt.
- Nachweise sind **Datei:Zeile** (aus Audit-Reports) oder **Command**, das du im Repo ausführen kannst.

---

## A) Bereits korrekt / weitgehend korrekt umgesetzt (IST Highlights)

1) **Licensing (Plans/Licenses/Subscriptions) ist end-to-end vorhanden**  
   - DB + API + Console + Home sind vorhanden.  
   - Nachweis: `audit_05.03.2026/07-feature-coverage.md:L11`

2) **Feature Flags + Rollouts + Kill-Switch sind implementiert**  
   - `plans.feature_flags` + `feature_overrides` + Console UI `/features`.  
   - Nachweis: `audit_05.03.2026/07-feature-coverage.md:L15`, `audit_05.03.2026/01-page-matrix.md` (Route `/features`)

3) **Auth-Härtung (JWKS + aud + iss + Roles) aktiv**  
   - Nachweis: `audit_05.03.2026/99-executive-summary.md:L36-L39`

4) **Pre-auth Rate-Limits + Body-Size Limit sind global aktiv**  
   - Nachweis: `audit_05.03.2026/99-executive-summary.md:L38-L39`

5) **Keycloak Pflicht-Clients vorhanden + Audience Mapper vorhanden** (mit Legacy-Duplizierung)  
   - Nachweis: `audit_05.03.2026/08-keycloak-config.md:L20-L40`

6) **Telemetry-Pipeline (Snapshots + Console Telemetry UI) vorhanden**  
   - Nachweis: `audit_05.03.2026/07-feature-coverage.md:L13`

---

## B) GAP-Matrix (Zielbild-Feature → IST → Delta)

> Prioritäten:
> - **P0** = Security/Prod-Sicherheit oder Blocker für reproduzierbares Deployment
> - **P1** = Zielbild-Kernfunktionalität (v6.3) fehlt/inkonsistent
> - **P2** = Nice-to-have / späterer Ausbau

| Zielbild-Feature (v6.3) | IST (Audit) | Delta (Was muss geändert/neu gebaut werden?) | Nachweis (IST) | Prio |
|---|---|---|---|---|
| **Greenfield-DB reproduzierbar nur via Alembic** (keine „hidden bootstrap steps“) | **Teilweise**: Alembic erzeugt nicht alle ORM Tabellen auf leerer DB (`devices`, `device_inventory` fehlen in Migration chain) | Entweder **(A)** dokumentierter, idempotenter Bootstrap (`bootstrap.sql`) **vor** `alembic upgrade`, oder **(B)** Migration-Kette reparieren (Initialmigration `create_table devices/device_inventory` + Rewire `0001` down_revision) | `audit_05.03.2026/06-migrations.md:L38-L40` | **P0** |
| **Agent-Registration ohne statischen API-Key im Client** (Device Token Flow) | `/api/v1/agent/register` benötigt `require_api_key` zusätzlich (Client-Secrets Risiko) | `agent/register` auf **Bootstrap-Key** (serverseitig) oder **User-Binding** umbauen; statischen API-Key entfernen; Device-Token only nach Register | `audit_05.03.2026/03-router-dependencies.md:L25` und Endpoint-Inventory | **P0** |
| **Updater/Release-Mechanik (Variante A GitHub Releases + Manifest)** | Repo enthält Installer/Bootstrapper + Manifest-Dateien, aber **GitHub-Var-A Verhalten ist im Audit nicht nachweisbar** | (1) Manifest-Schema auf v6.3 finalisieren (sha256, mandatory, min_supported). (2) Bootstrapper lädt Manifest via `releases/latest/download`. (3) Offline Installer Download + Hash verify. | Manifest/Installer Strukturen: `audit_05.03.2026/00-project-map.md:L51-L55, L262-L272` (Verhalten = **unknown**, Code nicht auditiert) | **P0** |
| **Home-Portal Download-Seite liefert stabile Links (GitHub latest/download)** | Home Route `/download` existiert, aber ohne nachweisbare API Calls (wahrscheinlich statische Links) | `/download` Seite so ändern, dass sie die **konstanten GitHub-URLs** anbietet (stable) + optional “copy sha256” | `audit_05.03.2026/_generated/page_matrix.csv` (Row `home,/download`), `audit_05.03.2026/01-page-matrix.md` | **P1** |
| **Device Version Visibility: desktop_version + updater_version** | DB `devices` enthält nur `agent_version` + `agent_channel` | `devices` erweitern: `desktop_version`, `updater_version`, optional `update_channel` + UI/DTOs updaten | `audit_05.03.2026/05-db-schema.md:L21` und `audit_05.03.2026/07-feature-coverage.md:L14` | **P1** |
| **Endpoint: POST /v1/client/status** (Desktop/Updater melden Versionen) | Kein `/client/status` Endpoint im Backend-Inventar | Neuen Router/Endpoint implementieren **oder** Agent Heartbeat erweitern; DB-Felder füllen + auditierbare Install-State Logs | Nachweis Abwesenheit: `rg -n "/api/v1/client/status" audit_05.03.2026/_generated/api_endpoints_inventory.csv` (soll 0 Treffer) | **P1** |
| **Update Channels (stable/beta/internal) pro Device/User steuerbar** | `devices.agent_channel` vorhanden, aber “update_channel” (desktop/updater) nicht nachweisbar | Feld `update_channel` (oder klare Reuse-Regel von `agent_channel`) + UI + Rollout Regeln | `audit_05.03.2026/05-db-schema.md:L21` | **P1** |
| **Support (Zammad) End-to-End: /v1/support/*** | Compose/Env existieren, aber **keine Support Router** | Support-Router implementieren (`/support/tickets`, `/reply`, `/attachments`, `/webhook`) **oder** Doku/Portal bereinigen | `audit_05.03.2026/07-feature-coverage.md:L17`; Router-Liste ohne support: `audit_05.03.2026/03-router-dependencies.md:L1-L12` | **P1** |
| **Notifications persistent + Read-State echt** | Notifications werden generiert; `mark_read` ist NOOP; keine Notification-Tabelle | DB Tabelle `notifications` + `notification_reads` (oder read_at) + API update + Console UI bleibt gleich aber echte Persistenz | `audit_05.03.2026/07-feature-coverage.md:L16` | **P2** |
| **Admin: Client Config remote** (`/admin/client-config`, DB `client_config`) | Nicht nachweisbar (keine DB Tabelle, keine API endpoints) | DB + API + Console Seite implementieren (Remote Config für Polling, auto-update policy, telemetry toggles) | Nachweis Abwesenheit: `rg -n "client-config" audit_05.03.2026/_generated/api_endpoints_inventory.csv` | **P2** |
| **Rules Engine Tabellen + Admin Rules UI** | Nicht nachweisbar (keine `rules_catalog`/`rule_findings`) | Tabellen + Router + Console Seite `Rules` hinzufügen (wenn Zielbild wirklich “ohne LLM” AI-features) | Quelle fehlt im Audit → **unknown** | **P2** |
| **Downloads/Knowledge Base persistent** (DB + Admin UI) | Knowledge Base Endpoint existiert, aber ohne DB Touch | Falls Zielbild: DB Tabellen `knowledge_base`, `downloads` + Admin CRUD | `audit_05.03.2026/_generated/api_endpoints_inventory.csv` Row `knowledge-base` hat `DbTablesTouched` leer | **P2** |
| **Realm Export versionieren** | optional, nicht umgesetzt | Realm JSON export in repo + deploy docs | `audit_05.03.2026/99-executive-summary.md:L49-L52` | **P2** |
| **Keycloak Client Konsolidierung (pcwaechter-* only)** | Legacy clients `console`, `home` zusätzlich vorhanden | Entscheidung + Cleanup (redirects/origins prüfen, tokens aud) | `audit_05.03.2026/08-keycloak-config.md:L32` | **P2** |

