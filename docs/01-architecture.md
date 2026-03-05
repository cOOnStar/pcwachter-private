# Architektur – PCWächter v6.2

## Komponenten

### Windows-Seite
| Komponente | Technologie | Funktion |
|---|---|---|
| **Desktop App** | WPF / .NET | OIDC Login (PKCE), Lizenzstatus, Feature-Gating, UI |
| **Agent / Service** | .NET Windows Service | Heartbeat, Telemetrie, Security Checks, Health/Score Upload, Remote Commands |
| **Updater** | .NET | Update-Metadaten abfragen, Installer herunterladen, Installations-Workflow |

### Server-Seite
| Komponente | Technologie | Funktion |
|---|---|---|
| **FastAPI Backend** | Python / FastAPI | AuthZ, Lizenzen, Geräte, Score, Rules Engine, Notifications, Admin APIs, Webhooks |
| **PostgreSQL** | Postgres 17 | Source of Truth für alle Geschäftsdaten |
| **Keycloak** | Keycloak 26 | Identity Provider (OIDC/OAuth2), Rollen, Tokens |
| **Home Portal** | Next.js 15 | Kundenportal (Lizenz, Billing, Devices, Support) |
| **Admin Console** | React / Vite | Admin-Oberfläche (Devices, Lizenzen, Rules, Ops) |
| **Stripe** | – | Billing, Subscriptions, Webhooks |
| **Zammad** | – | Support-Ticket-System (headless via API) |
| **Nginx Proxy Manager** | – | TLS-Terminierung, Reverse Proxy, Header-Forwarding |

## Datenflüsse

| Flow | Weg |
|---|---|
| **Login (OIDC)** | Client → Keycloak → Token → API JWT-Validierung |
| **Lizenzstatus / Feature-Flags** | Client → `GET /v1/license/status` → Plan + feature_flags + overrides |
| **Heartbeat / Telemetrie** | Agent → `POST /v1/agent/heartbeat` + `/v1/agent/telemetry` → DB |
| **Health / Score** | Agent → `POST /v1/agent/health` → Rules Engine → Findings/Score → DB |
| **Security Scan** | Agent → `POST /v1/agent/security-scan` → DB + Notifications |
| **Notifications** | API → `GET /v1/notifications` → Portal/Desktop |
| **Support** | Portal → `POST /v1/support/tickets` → Zammad API (Proxy) |
| **Billing** | Portal → `POST /v1/payments/create-checkout` → Stripe → Webhook → Subscription |

## Lizenzsystem

| Plan | Preis | Laufzeit | Geräte |
|---|---|---|---|
| `trial` | 0 € | 14 Tage | 1 |
| `standard` | 4,99 €/Monat | 30 Tage | 3 |
| `professional` | 49,99 €/Jahr | 365 Tage | ∞ |
| `unlimited` | – | ∞ | ∞ |
| `custom` | – | individuell | individuell |

**Regeln:**
- Trial nur einmal pro User (DB-geprüft, nicht Keycloak)
- Device Binding: `device_install_id` → Lizenz
- Grace Period: `grace_period_days` pro Plan konfigurierbar
- `max_devices`: distinct active `device_install_id` pro User/Plan
- Admin kann Accounts/Subscriptions blocken

## Regelbasierte Intelligenz (Rules Engine)

**Inputs:** Telemetrie (CPU/RAM/Disk), Konfiguration, Security-State, Trenddaten (7/30/90 Tage), Crash Reports

**Outputs:** Findings + Severity, Reasons (Messwerte), Recommendations, optional Auto-Fix Actions

**Wichtig:** Keine LLM-Anbindung – alle Analysen sind regelbasiert und deterministisch.

## Feature-Flag-System

```
Final Features = plan_flags AND override.enabled AND rollout AND version_gate AND platform_gate
```

1. `plans.feature_flags` (JSONB) → Darf grundsätzlich (Plan-Level)
2. `feature_overrides` → Kill-Switch / stufenweiser Rollout (Ops-Level)
