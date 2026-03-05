# PCWächter Admin Console

## Overview

The Admin Console is a React-based single-page application (SPA) for managing PCWächter installations.
It lives in `server/console/` and communicates with the API at `/api/v1/console/ui/*`.

## Tech Stack

| Layer | Technology |
|---|---|
| UI Framework | React 19 + TypeScript + Vite |
| Styling | Tailwind v4 (`@tailwindcss/vite` plugin, no PostCSS) |
| Components | shadcn/ui (manual setup with Radix UI primitives) |
| Data Fetching | @tanstack/react-query v5 |
| Auth | keycloak-js PKCE (`onLoad: "login-required"`, `pkceMethod: "S256"`) |
| Routing | react-router-dom v7 |
| Icons | lucide-react |
| Charts | recharts |

## Environment Variables

Copy `.env.example` to `.env` and configure:

```env
# Base URL including /api/v1 (required)
VITE_API_BASE_URL=https://api.xn--pcwchter-2za.de/api/v1

# Keycloak
VITE_KEYCLOAK_URL=https://login.xn--pcwchter-2za.de
VITE_KEYCLOAK_REALM=pcwaechter-prod
VITE_KEYCLOAK_CLIENT_ID=console
```

## Local Development

```bash
cd server/console
npm install
npm run dev
# → http://localhost:5173
```

## Build

```bash
npm run build
# Output: dist/
```

## Keycloak Setup

1. Create client `console` in realm `pcwaechter-prod`
2. Client type: `Public` (PKCE, no secret)
3. Valid redirect URIs: `https://console.xn--pcwchter-2za.de/*`
4. Web origins: `https://console.xn--pcwchter-2za.de`

## RBAC

The console supports two role sets:

| Role | Access Level |
|---|---|
| `pcw_admin` | Full admin (read + write) |
| `pcw_console` | Read-only (no write/action endpoints) |
| `owner` | Full admin (legacy role) |
| `admin` | Full admin (legacy role) |
| `pcw_support` | Support (limited, frontend-only guard) |
| `user` | No access |

### Helper Functions

```tsx
const { hasAccess, isAdmin, isSupport } = useAuth();

hasAccess()  // true for: pcw_admin | pcw_console | owner | admin
isAdmin()    // true for: pcw_admin | owner | admin
isSupport()  // true for: pcw_support
```

## API Paths

All calls go to `VITE_API_BASE_URL + /console/ui/...`:

| Page | Endpoint |
|---|---|
| Dashboard | `GET /console/ui/dashboard` |
| Devices | `GET /console/ui/devices` |
| Device Detail | `GET /console/ui/devices/{id}/detail` |
| Device Block | `POST /console/ui/devices/{id}/block` |
| Licenses | `GET /console/ui/licenses` |
| Generate Licenses | `POST /console/ui/licenses/generate` |
| Revoke License | `POST /console/ui/licenses/{key}/revoke` |
| Block License | `POST /console/ui/licenses/{key}/block` |
| Unblock License | `POST /console/ui/licenses/{key}/unblock` |
| Patch License | `PATCH /console/ui/licenses/{key}` |
| Plans | `GET /console/ui/plans` |
| Feature Overrides | `GET /console/ui/features/overrides` |
| Upsert Override | `POST /console/ui/features/overrides` |
| Kill-Switch | `POST /console/ui/features/{key}/disable` |
| Accounts | `GET /console/ui/accounts` |
| Telemetry | `GET /console/ui/telemetry` |
| Audit Log | `GET /console/ui/audit-log` |
| Notifications | `GET /console/ui/notifications` |
| Server | `GET /console/ui/server/host` |

Legacy paths (without `/api/v1`) are also served for backward compatibility
and return `Deprecation: true` + `Sunset` headers.

## Feature Overrides

The `feature_overrides` table supports scoped rollouts:

| Scope | target_id | Description |
|---|---|---|
| `global` | NULL | Applies to all users/devices |
| `plan` | plan_id | Applies to a specific plan |
| `user` | user_id | Applies to a specific user |
| `device` | device_install_id | Applies to a specific device |

The Kill-Switch (`POST /features/{key}/disable`) creates a global override with
`enabled=false, rollout_percent=0, scope='global', target_id=NULL`.

## Pages

| Route | Page | Admin Only |
|---|---|---|
| `/` | Dashboard | No |
| `/devices` | Devices list | No |
| `/devices/:id` | Device detail + block/unblock | No (block: admin) |
| `/licenses` | License management | No (generate/block: admin) |
| `/plans` | Plan configuration | Yes |
| `/features` | Feature rollouts + kill-switch | No (write: admin) |
| `/accounts` | User accounts + role mgmt | No (write: admin) |
| `/telemetry` | Telemetry snapshots + chart | No |
| `/audit` | Audit log | No |
| `/notifications` | System notifications | No |
| `/server` | Host + Docker containers | No |
