# Audit B) Page Matrix (Frontend IST)

## Methode / Nachweis
- Console-Routen aus `server/console/src/App.tsx` (React Router).
- Home-Routen aus `server/home/src/app/**/page.tsx` (Next.js App Router).
- Service-Calls aus:
  - `server/console/src/app/services/api-service.ts`
  - `server/home/src/lib/api.ts`
  - `server/home/src/app/api/*/route.ts`
- Maschinenlesbare Quelle: `docs/audit/_generated/page_matrix.csv`.

## Page Matrix

| App | Route/Path | Page/Component Datei | Verwendete Services/Calls | Auth Requirement | UI Features | Status | Link zu Screenshot/Mock/Figma |
|---|---|---|---|---|---|---|---|
| console | `/` | `server/console/src/app/pages/DashboardPage.tsx` | `getDashboard` | Keycloak login-required; backend enforces roles | Error Banner, Loading Skeleton, Pagination | exists | unknown |
| console | `/accounts` | `server/console/src/app/pages/AccountsPage.tsx` | `getAccounts`, `updateAccountRole` | Keycloak login-required; backend enforces roles | Admin-gated Actions, DataTable, Error Banner, Pagination, Search/Filter | exists | unknown |
| console | `/audit` | `server/console/src/app/pages/AuditLogPage.tsx` | `getAuditLog` | Keycloak login-required; backend enforces roles | DataTable, Error Banner, Pagination | exists | unknown |
| console | `/devices` | `server/console/src/app/pages/DevicesPage.tsx` | `getDevices` | Keycloak login-required; backend enforces roles | DataTable, Error Banner, Pagination, Search/Filter | exists | unknown |
| console | `/devices/:deviceId` | `server/console/src/app/pages/DeviceDetailPage.tsx` | `blockDevice`, `getDeviceDetail`, `unblockDevice` | Keycloak login-required; backend enforces roles | Admin-gated Actions, Error Banner, Loading Skeleton, Pagination | exists | unknown |
| console | `/features` | `server/console/src/app/pages/FeatureRolloutsPage.tsx` | `disableFeature`, `getFeatureOverrides`, `upsertFeatureOverride` | Keycloak login-required; backend enforces roles | Admin-gated Actions, Dialog/Modal, Empty State, Error Banner, Loading Skeleton, Pagination | exists | unknown |
| console | `/licenses` | `server/console/src/app/pages/LicensesPage.tsx` | `blockLicense`, `generateLicenses`, `getLicenses`, `revokeLicense`, `unblockLicense` | Keycloak login-required; backend enforces roles | Admin-gated Actions, Copy to Clipboard, DataTable, Dialog/Modal, Error Banner, Pagination, Search/Filter | exists | unknown |
| console | `/notifications` | `server/console/src/app/pages/NotificationsPage.tsx` | `getNotifications`, `markNotificationRead` | Keycloak login-required; backend enforces roles | Empty State, Error Banner, Loading Skeleton, Pagination | exists | unknown |
| console | `/plans` | `server/console/src/app/pages/PlansPage.tsx` | `getPlans`, `upsertPlan` | Keycloak login-required + admin-only route in UI | Dialog/Modal, Error Banner, Loading Skeleton, Pagination | exists | unknown |
| console | `/server` | `server/console/src/app/pages/ServerPage.tsx` | `getContainers`, `getHostInfo` | Keycloak login-required; backend enforces roles | Error Banner, Loading Skeleton, Pagination | exists | unknown |
| console | `/telemetry` | `server/console/src/app/pages/TelemetryPage.tsx` | `getTelemetry`, `getTelemetryChart` | Keycloak login-required; backend enforces roles | DataTable, Error Banner, Pagination | exists | unknown |
| home | `/` | `server/home/src/app/page.tsx` | none | public | Redirect, SSR/Next.js Route | exists | unknown |
| home | `/account` | `server/home/src/app/account/page.tsx` | `getLicenseStatus` | NextAuth session required via `account/layout.tsx` | Navigation Links, SSR/Next.js Route, Suspense | exists | unknown |
| home | `/account/billing` | `server/home/src/app/account/billing/page.tsx` | `LOCAL:/api/portal` | NextAuth session required via `account/layout.tsx` | Error handling, Fetch call, SSR/Next.js Route | exists | unknown |
| home | `/download` | `server/home/src/app/download/page.tsx` | none | public | Redirect, SSR/Next.js Route | exists | unknown |
| home | `/pricing` | `server/home/src/app/pricing/page.tsx` | none | public | Redirect, SSR/Next.js Route | exists | unknown |
| website? | `unknown` | `src/components/Dashboard.jsx` | none | unknown | Standalone component (not wired to app router/build) | partial | unknown |

## Kurzfazit
- Console: 11 produktive Routen.
- Home: 5 App-Routen (davon mehrere Redirect/Marketing).
- Zusätzliche Website-App ist im Repo nicht eindeutig verdrahtet (`src/components/Dashboard.jsx`), daher `partial/unknown`.
