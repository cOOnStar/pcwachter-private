# Keycloak – vollständige technische Einstellungen

## Realm

| Einstellung | Wert |
|---|---|
| Realm Name | `pcwaechter-prod` |
| Issuer | `https://login.pcwächter.de/realms/pcwaechter-prod` |
| Display Name | PCWächter |

## Password Policy (Produktion)

- Mindestlänge: 12 Zeichen
- Brute Force Protection: **ON**
- Lockout/Backoff Policy aktiv
- Admins: OTP Required Action
- Support: OTP empfohlen

## Rollen & Gruppen

### Realm Roles
| Rolle | Beschreibung |
|---|---|
| `pcw_user` | Standard-User (Portal, Lizenzstatus) |
| `pcw_admin` | Vollzugriff Console + Admin-Aktionen |
| `pcw_console` | Console-Lesezugriff |
| `pcw_support` | Support-Ansicht |
| `pcw_agent` | Agent API (normalerweise API-Key) |

### Gruppen & Role Mapping
| Gruppe | Rollen |
|---|---|
| `users` | `pcw_user` |
| `admins` | `pcw_admin`, `pcw_console` |
| `support` | `pcw_support`, optional `pcw_console` |
| `beta-testers` | optional (Experiment scope) |

## Clients

### A) `pcwaechter-desktop` (Public, PKCE)
| Einstellung | Wert |
|---|---|
| Client Type | Public |
| Standard Flow | ON |
| PKCE | S256 required |
| Direct Access Grants | OFF |
| Redirect URIs | `http://localhost:8765/callback` |
| Post Logout Redirect | `http://localhost:8765/*` |
| Scopes | `profile`, `email`, optional `offline_access` |
| Refresh Tokens | ON (Desktop UX) |

### B) `home` (Confidential)
| Einstellung | Wert |
|---|---|
| Client Type | Confidential |
| Standard Flow | ON |
| Direct Access Grants | OFF |
| Redirect URIs | `https://home.pcwächter.de/*`, `http://localhost:13001/*` |
| Web Origins | `https://home.pcwächter.de`, `http://localhost:13001` |
| Post Logout Redirect | `https://home.pcwächter.de/*` |
| Client Secret | Server-side only (in `.env` als `AUTH_KEYCLOAK_SECRET`) |

### C) `console` (Public, PKCE)
| Einstellung | Wert |
|---|---|
| Client Type | Public |
| Standard Flow | ON |
| PKCE | S256 recommended |
| Direct Access Grants | OFF |
| Redirect URIs | `https://console.pcwächter.de/*`, `http://localhost:13000/*` |
| Web Origins | `https://console.pcwächter.de`, `http://localhost:13000` |
| Post Logout Redirect | `https://console.pcwächter.de/*` |
| MFA | Empfohlen für `pcw_admin` |

### D) `pcwaechter-api` (Audience/Resource)
Audience Mapper → Access Tokens von `home`/`console`/`desktop` enthalten:
```
aud: ["pcwaechter-api"]
```
Backend validiert `aud` strict.

## Token Claims (erforderlich)
- `sub`, `iss`, `aud` (→ `pcwaechter-api`), `exp`, `iat`
- `realm_access.roles`
- optional: `groups`, `email`

## Token Lifetimes (Produktion)
| Token | Lifetime |
|---|---|
| Access Token | 5–15 Minuten |
| Refresh Token | 7–30 Tage |
| Offline Token | Desktop only (optional) |

## Logout / SSO
Frontchannel Logout URLs:
- `https://home.pcwächter.de/*`
- `https://console.pcwächter.de/*`
- Desktop: Tokens lokal löschen + optional Revocation

## Security Defaults
- Brute Force Protection: ON
- Password Policy: min 12 Zeichen
- Admins: OTP Required Action
- Sessions: IP-Binding optional
