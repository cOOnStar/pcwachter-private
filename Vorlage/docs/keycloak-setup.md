# Keycloak Setup (prod) – PCWächter

## 1) Realm
- Realm: `pcwaechter-prod`
- Issuer: `https://login.pcwächter.de/realms/pcwaechter-prod`

## 2) Realm Roles (anlegen)
- `pcw_user`
- `pcw_admin`
- `pcw_support`
- `pcw_console`
- `pcw_agent`

## 3) Gruppen (optional) + Rollenmapping
- `users` → `pcw_user`
- `admins` → `pcw_admin`, `pcw_console`
- `support` → `pcw_support`, optional `pcw_console`

## 4) Clients

### 4.1 pcwaechter-desktop
- Client Type: Public
- Flow: Authorization Code + PKCE (S256 required)
- Redirect URI: `http://localhost:8765/callback`
- Direct Access Grants: OFF
- Scopes: `profile`, `email` (+ optional `offline_access`)

### 4.2 pcwaechter-home
- Client Type: Confidential (empfohlen)
- Redirect URIs: `https://home.pcwächter.de/*`
- Web Origins: `https://home.pcwächter.de`
- Post Logout Redirect URIs: `https://home.pcwächter.de/*`

### 4.3 pcwaechter-console
- Client Type: Confidential (empfohlen)
- Redirect URIs: `https://console.pcwächter.de/*`
- Web Origins: `https://console.pcwächter.de`
- Post Logout Redirect URIs: `https://console.pcwächter.de/*`

### 4.4 Audience für API
- Ziel: Access Tokens sollen `aud` = `pcwaechter-api` enthalten.
- Lösung: Audience Mapper / Client Scope hinzufügen:
  - Audience: `pcwaechter-api`

## 5) Token Claims
- `sub`, `iss`, `aud`, `exp`
- `realm_access.roles`
- optional: `groups`, `email`

## 6) Security Defaults (empfohlen)
- Brute force protection: ON
- Password policy: min 12
- Admins: OTP required action
