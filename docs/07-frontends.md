# Frontends – Admin Console & Home Portal

## Admin Console (React / Vite)

**URL:** `https://console.xn--pcwchter-2za.de`
**Auth:** Keycloak OIDC, Rolle `pcw_admin` oder `pcw_console`
**Build:** Vite, statische SPA – Env-Vars werden beim Build eingebacken (`VITE_*`)

### Seiten / Routen

| Route | Komponente | Auth | Beschreibung |
|---|---|---|---|
| `/` | Dashboard | Alle | KPIs: aktive Geräte, Lizenzen, Telemetrie-Trend |
| `/devices` | DeviceList | Alle | Geräteliste mit Status, Score, letzte Aktivität |
| `/devices/:id` | DeviceDetail | Alle | Detail: Telemetrie, Inventar, Score-Verlauf |
| `/telemetry` | TelemetryView | Alle | Rohe Telemetrie-Snapshots, Chart |
| `/licenses` | LicenseList | Alle | Lizenzen + Status |
| `/licenses/new` | LicenseCreate | `pcw_admin` | Neue Lizenz erstellen |
| `/plans` | PlanManager | `pcw_admin` | Pläne verwalten, Feature-Flags konfigurieren |
| `/accounts` | AccountList | `pcw_admin` | Keycloak-Accounts, Rollen-Management |
| `/notifications` | Notifications | Alle | System-Benachrichtigungen |
| `/activity` | ActivityFeed | Alle | Aktivitätsfeed (Audit-Light) |
| `/audit` | AuditLog | `pcw_admin` | Vollständiger Audit-Log |
| `/search` | GlobalSearch | Alle | Geräte, Lizenzen, Accounts suchen |
| `/knowledge-base` | KnowledgeBase | Alle | Interne Wissensdatenbank |
| `/server` | ServerOverview | `pcw_admin` | Host-Metriken, Docker-Container, Services |
| `/database` | DBInspector | `pcw_admin` | DB-Inhalte (Hosts, Payloads) |
| `/preview` | FigmaPreview | Alle | Figma-Design-Preview |

### Keycloak-Client

| Einstellung | Wert |
|---|---|
| Client ID | `console` |
| Flow | Authorization Code + PKCE |
| Redirect URI | `https://console.xn--pcwchter-2za.de/*` |
| Rollen | `pcw_admin`, `pcw_console` |

---

## Home Portal (Next.js 15)

**URL:** `https://home.xn--pcwchter-2za.de`
**Auth:** NextAuth v5 mit Keycloak Provider
**Build:** Next.js App Router, Server Components + Client Components

### Seiten / Routen

| Route | Beschreibung |
|---|---|
| `/` | Landing / Übersicht (Lizenzstatus, schnelle Links) |
| `/login` | Login-Redirect zu Keycloak |
| `/dashboard` | Benutzer-Dashboard: Lizenz, Geräte, Feature-Status |
| `/license` | Meine Lizenz (Plan, Ablauf, Geräte-Binding) |
| `/devices` | Eigene Geräte verwalten |
| `/billing` | Stripe-Abo: Plan wechseln, Zahlungsmethode, Rechnungen |
| `/billing/checkout` | Stripe Checkout Session |
| `/support` | Support-Tickets (Zammad-Proxy) |
| `/support/new` | Neues Ticket erstellen |
| `/settings` | Konto-Einstellungen |

### Keycloak-Client

| Einstellung | Wert |
|---|---|
| Client ID | `home` |
| Flow | Authorization Code + PKCE |
| Redirect URI | `https://home.xn--pcwchter-2za.de/*` |
| Rolle | `pcw_user` |

### API-Zugriff

- **Browser → API:** `NEXT_PUBLIC_API_URL` (direkt, Bearer Token via NextAuth Session)
- **Server → API:** `API_INTERNAL_URL` (Container-intern, schneller)
- Feature-Gating: `/status` Endpoint → `feature_flags` bestimmen UI-Sichtbarkeit

---

## Build-Zeit Variablen

### Console (Vite)

| Variable | Zweck |
|---|---|
| `VITE_API_URL` | API Base-URL für Browser |
| `VITE_KEYCLOAK_URL` | Keycloak-URL für Browser |
| `VITE_KEYCLOAK_REALM` | Realm-Name |
| `VITE_KEYCLOAK_CLIENT_ID` | Client ID (`console`) |

### Home (Next.js)

| Variable | Zweck |
|---|---|
| `NEXT_PUBLIC_API_URL` | API Base-URL für Browser |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | Stripe Public Key für Browser |
