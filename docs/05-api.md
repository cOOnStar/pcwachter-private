# API Endpoints (FastAPI)

Base URL: `https://api.xn--pcwchter-2za.de` (Prod) / `http://localhost:18080` (lokal)

## Public

| Method | Pfad | Auth | Beschreibung |
|---|---|---|---|
| GET | `/health` | – | Health Check |
| GET | `/public/plans` | – | Aktive Pläne (für Pricing-Seite) |

## Agent (API-Key via `X-Agent-Api-Key`)

| Method | Pfad | Beschreibung |
|---|---|---|
| POST | `/agent/register` | Gerät registrieren / aktualisieren |
| POST | `/agent/heartbeat` | Heartbeat senden |
| POST | `/agent/inventory` | Inventar hochladen |

## Telemetry (API-Key via `X-Api-Key` oder `X-Agent-Api-Key`)

| Method | Pfad | Beschreibung |
|---|---|---|
| POST | `/telemetry/update` | Update-Ergebnis melden |
| POST | `/telemetry/snapshot` | Telemetrie-Snapshot senden (memory/ssd/antivirus) |

## Admin (API-Key via `X-Api-Key`)

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/admin/devices/overview` | Geräteübersicht mit letzten Snapshots |
| DELETE | `/admin/devices/{device_install_id}` | Gerät löschen |
| DELETE | `/admin/devices/{device_install_id}/snapshots/{snapshot_id}` | Snapshot löschen |

## License / Status (Bearer Token oder API-Key)

| Method | Pfad | Auth | Beschreibung |
|---|---|---|---|
| POST | `/activate` | API-Key | Lizenz aktivieren |
| GET | `/me` | Bearer | Eigene Lizenzinfo |
| GET | `/status` | Bearer/API-Key | Lizenz-Status + Feature-Flags |

## Payments / Stripe (Bearer Token)

| Method | Pfad | Beschreibung |
|---|---|---|
| POST | `/create-checkout` | Stripe Checkout Session erstellen |
| POST | `/portal` | Stripe Customer Portal öffnen |
| POST | `/webhook` | Stripe Webhook (kein Auth – Signature Verify) |

## Console UI (Bearer Token – Rolle `pcw_admin` oder `pcw_console`)

### Dashboard & Monitoring

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/ui/dashboard` | Dashboard KPIs |
| GET | `/ui/activity-feed` | Aktivitätsfeed |
| GET | `/ui/audit-log` | Audit-Log |
| GET | `/ui/notifications` | Benachrichtigungen |
| GET | `/ui/search` | Globale Suche |

### Geräteverwaltung

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/ui/devices` | Geräteliste |
| GET | `/ui/devices/{device_id}` | Gerätedetail |
| GET | `/ui/devices/{device_id}/inventory/latest` | Letztes Inventar |

### Telemetrie

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/ui/telemetry` | Telemetrie-Einträge |
| GET | `/ui/telemetry/chart` | Telemetrie Chart-Daten |

### Lizenzen & Pläne

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/ui/licenses` | Lizenzen |
| POST | `/ui/licenses` | Lizenz erstellen |
| PATCH | `/ui/licenses/{license_key}/revoke` | Lizenz sperren |
| GET | `/ui/plans` | Pläne (Admin-Ansicht) |
| PUT | `/ui/plans/{plan_id}` | Plan anlegen/aktualisieren |

### Account-Verwaltung (nur `pcw_admin`)

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/ui/accounts` | Keycloak-Accounts |
| PATCH | `/ui/accounts/{account_id}/role` | Account-Rolle ändern |

### Server & Infrastruktur

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/ui/server/host` | Server-Host Metriken |
| GET | `/ui/server/containers` | Docker Container Status |
| GET | `/ui/server/services` | Service-Übersicht |

### Datenbank-Ansicht

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/ui/database/hosts` | DB Geräte-Hosts |
| GET | `/ui/database/payloads` | DB Telemetrie Payloads |

### Sonstiges

| Method | Pfad | Beschreibung |
|---|---|---|
| GET | `/ui/knowledge-base` | Wissensdatenbank |
| GET | `/ui/preview` | Figma Preview Snapshot |

## Auth-Rollen (Keycloak Realm: `pcwaechter-prod`)

| Rolle | Zugriff |
|---|---|
| `pcw_admin` | Vollzugriff auf Console + Owner-Aktionen |
| `pcw_console` | Console-Lesezugriff |
| `pcw_user` | Home Portal, Lizenzstatus |
| `pcw_agent` | Agent API (normalerweise via API-Key) |
| `pcw_support` | Support-Ansicht |

## Auth-Methoden

| Methode | Header | Verwendung |
|---|---|---|
| Bearer Token | `Authorization: Bearer <token>` | Browser-Clients (Console, Home Portal) |
| API-Key | `X-Api-Key: <key>` | Externe Tools, Admin-Zugriff |
| Agent API-Key | `X-Agent-Api-Key: <key>` | Windows-Agent |
