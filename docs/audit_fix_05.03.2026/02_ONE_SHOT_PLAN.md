# PCWächter – Umsetzungsplan „One-Shot“ (Audit → Zielbild v6.3, Variante A)

> Variante A = Updates über GitHub Releases (public-release Repo) + Live-Installer, der den neuesten Offline-Installer lädt.

## 0) Vorab: Scope & Annahmen (aus Audit ableitbar)

- Backend Router vorhanden: `admin.py`, `agent.py`, `console.py`, `features.py`, `license.py`, `payments.py`, `telemetry.py`  
  Nachweis: `audit_05.03.2026/03-router-dependencies.md:L1-L12`
- DB ORM umfasst 9 Tabellen (`models.py`), u. a. `devices`, `device_inventory`, `feature_overrides`  
  Nachweis: `audit_05.03.2026/05-db-schema.md:L15-L33`
- Updater/Versioning ist **nicht vollständig** (kein `desktop_version`/`updater_version`)  
  Nachweis: `audit_05.03.2026/07-feature-coverage.md:L14`
- Alembic Greenfield ist **nicht reproduzierbar**, weil `devices`/`device_inventory` nicht initial erstellt werden  
  Nachweis: `audit_05.03.2026/06-migrations.md:L38-L40`

**unknown / fehlende Quellen (Audit enthält sie nicht):**
- Konkreter Inhalt von `server/api/app/models.py`, `server/api/app/main.py`, Frontend-Dateien, Installer-Code.  
  → Audit referenziert Pfade, aber die Quelltexte sind nicht in `audit_05.03.2026.zip`.

---

## 1) Reihenfolge mit Abhängigkeiten (koordinierter Durchlauf)

### Phase 1 — DB/Fundament (P0)
1. **Greenfield-Bootstrap schließen** (damit neue Umgebungen reproduzierbar sind)
2. **Device Version Columns** (`desktop_version`, `updater_version`, optional `update_channel`) + Migration

### Phase 2 — Backend APIs (P0/P1)
3. **/agent/register**: API-Key Abhängigkeit entfernen → Bootstrap-Key oder user-binding
4. **/client/status** Endpoint hinzufügen (Desktop/Updater melden Versionen + Channel)
5. **Support Router** `/api/v1/support/*` implementieren (falls Zielbild beibehalten)

### Phase 3 — Frontends (P1)
6. Console Device UI erweitern (Versionsfelder anzeigen)
7. Home `/download` Seite auf GitHub Release Links umstellen
8. (Optional) Home Support Pages hinzufügen/verdrahten

### Phase 4 — Release/CI (P0/P1)
9. Public Release Repo (`pcwaechter-public-release`) strukturieren + Manifest + Release Assets
10. Smoke Tests / Checks

---

## 2) Konkrete Dateiänderungen (Dateipfade + genaue Deltas)

> Weil die Quelltexte nicht im Audit-Zip enthalten sind, sind die **Edit-Punkte als präzise Pfade + „insert near …“** formuliert.  
> Wo möglich: Audit nennt Startzeilen/Handler-Locations, die du als Anker nutzen kannst.

### 2.1 DB / Alembic

#### 2.1.1 Greenfield-Bootstrap (empfohlen: idempotent SQL)
**Neu anlegen:**
- `server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql`  *(idempotent: CREATE TABLE IF NOT EXISTS …)*  
**Doku ergänzen:**
- `docs/deploy.md` oder `docs/06-db.md`: Abschnitt „Greenfield Bootstrap“  
**Grund:** Migration `20260227_0001` setzt `devices` voraus.  
Nachweis: `audit_05.03.2026/06-migrations.md:L38-L40`.

#### 2.1.2 Device Version Columns
**Ändern:**
- `server/api/app/models.py` → `Device` Model um Felder erweitern  
  - `desktop_version: string nullable`
  - `updater_version: string nullable`
  - `update_channel: string nullable` *(oder Reuse von `agent_channel` dokumentieren)*
  - Nachweis IST devices columns: `audit_05.03.2026/05-db-schema.md:L21`

**Neu anlegen (Migration):**
- `server/api/alembic/versions/20260305_0010_add_desktop_updater_versions.py`  
  - `op.add_column('devices', sa.Column('desktop_version', sa.String(), nullable=True))`  
  - `op.add_column('devices', sa.Column('updater_version', sa.String(), nullable=True))`  
  - `op.add_column('devices', sa.Column('update_channel', sa.String(), nullable=True))`

> Hinweis: Alembic Naming: IST hat `20260305_0009` als Head.  
> Nachweis: `audit_05.03.2026/06-migrations.md:L27-L33`

---

### 2.2 Backend / FastAPI

#### 2.2.1 Endpoint /client/status (neu)
**Neu anlegen:**
- `server/api/app/routers/client.py`  
  - `POST /api/v1/client/status`
  - Auth: **JWT user** ODER **Device Token** (je nachdem welche Komponente meldet)

**Ändern:**
- `server/api/app/main.py` (oder Router include file) → neuen Router registrieren  
  - Nachweis bestehender Router-Set: `audit_05.03.2026/03-router-dependencies.md:L1-L12`

**DB-Write:**
- Update `devices.desktop_version`, `devices.updater_version`, `devices.update_channel` (und `last_seen_at`) anhand `device_install_id`.

**Nachweis, dass Endpoint aktuell fehlt:**
- Command: `rg -n "/api/v1/client/status" audit_05.03.2026/_generated/api_endpoints_inventory.csv`

#### 2.2.2 /agent/register Auth umbauen (P0)
**IST:**
- `/api/v1/agent/register` benötigt `require_api_key` + `require_agent_auth`.  
  Nachweis: `audit_05.03.2026/03-router-dependencies.md:L25`

**Delta:**
- Entferne statischen API-Key aus Client-Pfaden:
  - Option A (empfohlen): `X-Install-Key` (bootstrap key) → nur serverseitig verteilt / short-lived
  - Option B: user-binding via Desktop JWT + Agent register als „child“
- Anpassung an `require_agent_auth` (oder neues `require_agent_bootstrap`) nötig.

**Dateien (aus Audit ableitbar):**
- `server/api/app/routers/agent.py`
- `server/api/app/security_*.py` (Audit nennt `security_jwt.py` als JWT Layer; genauer Pfad in Code prüfen)  
  Nachweis JWT Layer existiert: `audit_05.03.2026/99-executive-summary.md:L36-L39`

#### 2.2.3 Support Router /v1/support/* (P1)
**IST:**
- Keine Support Router nachweisbar.  
  Nachweis: `audit_05.03.2026/07-feature-coverage.md:L17`

**Delta:**
- Neu: `server/api/app/routers/support.py`
  - `GET /api/v1/support/tickets`
  - `POST /api/v1/support/tickets`
  - `GET /api/v1/support/tickets/{id}`
  - `POST /api/v1/support/tickets/{id}/reply`
  - `POST /api/v1/support/attachments`
  - `POST /api/v1/support/webhook` (shared secret check)
- Env Keys verwenden, die laut Audit bereits existieren, aber nicht im Zip sichtbar sind (**unknown**): `.env(.example)`/Compose prüfen.

---

### 2.3 Frontends

#### 2.3.1 Console: Devices List + Device Detail erweitern
**IST Seiten/Dateien:**
- `/devices` → `server/console/src/app/pages/DevicesPage.tsx`  
- `/devices/:deviceId` → `server/console/src/app/pages/DeviceDetailPage.tsx`  
Nachweis: `audit_05.03.2026/01-page-matrix.md` (Routes `/devices`, `/devices/:deviceId`)

**Delta:**
- UI: zusätzlich anzeigen  
  - Desktop Version
  - Updater Version
  - Update Channel
- API Service: `getDevices`, `getDeviceDetail` Response DTO erweitern.

#### 2.3.2 Home: Download-Seite
**IST:**
- Route `/download` existiert ohne Service Calls.  
  Nachweis: `audit_05.03.2026/_generated/page_matrix.csv` (home,/download)

**Delta:**
- Links auf GitHub Releases:
  - Offline Installer: `.../releases/latest/download/PCWaechter-Offline-Setup.exe`
  - Live Installer: `.../releases/latest/download/PCWaechter-Live-Installer.exe`
  - Manifest: `.../releases/latest/download/installer-manifest.json`
- Optional: JS Fetch Manifest → zeige `latest_version`, `released_at`, `sha256`.

---

### 2.4 Public Release Repo (pcwaechter-public-release) – Variante A

**Neu in public-release Repo:**
- `/installer-manifest.json` als Release Asset (nicht nur im Git)
- GitHub Action: baut **Offline Installer** + **Live Installer** und lädt beide als Assets hoch
- Live Installer Verhalten:
  1) lädt Manifest (latest)
  2) lädt Offline Installer (URL aus Manifest oder per latest/download)
  3) verifiziert sha256
  4) startet Offline Installer silent/interactive

**unknown:** aktuelle CI/Build-Pipeline & Signierung sind im Audit nicht beschrieben → in Repo prüfen.

---

## 3) Nötige Migration Steps (idempotent)

1) **Bestands-DB:**  
   - Migration `20260305_0010` ausführen:
     - `docker exec pcw-api alembic upgrade head`
2) **Greenfield-DB (neu):**
   - `psql -f server/api/alembic/bootstrap/0000_create_devices_and_inventory.sql`
   - `alembic upgrade head`

---

## 4) Env / Keycloak / Compose Anpassungen (Delta)

### Env
- **NEU** (für Agent Bootstrap-Key): `PCW_AGENT_BOOTSTRAP_KEY` *(Name frei, aber konsistent)*  
- **NEU** (optional für Home Download): `NEXT_PUBLIC_RELEASE_BASE_URL` (z. B. GitHub Repo URL)
- **Support/Zammad**: Env existiert laut Audit, aber Keys im Zip nicht sichtbar (**unknown**) → `.env.example` prüfen.

### Keycloak
- Optional P2: Legacy Clients `console`/`home` entfernen (nach Migration), wenn `pcwaechter-*` stabil.  
  Nachweis Legacy: `audit_05.03.2026/08-keycloak-config.md:L32`

### Compose
- Keine zwingenden Änderungen aus Audit ableitbar (**unknown**) – hängt davon ab, ob Support-Router neue Env braucht.

