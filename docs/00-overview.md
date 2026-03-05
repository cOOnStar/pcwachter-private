# PCWächter – Technische Gesamtübersicht v6.2

## Leitlinien & Grundannahmen

### Keine LLM / keine externe Modellanbindung
- Keine KI-Modelle (kein ChatGPT/Claude/Anthropic API).
- „KI"-Features sind **regelbasierte Intelligenz** (Rules Engine).
- Ergebnisse sind deterministisch und auditierbar.

### Source of Truth
| Komponente | Verantwortung |
|---|---|
| **Keycloak** | Identität, Authentifizierung, Tokens, Rollen/Gruppen |
| **FastAPI + Postgres** | Business-Logik, Daten, Autorisierung, Lizenz-/Device-/Score-/Support-/Ops-Steuerung |
| **Clients/Portale** | Darstellung + Feature-Gating auf Basis API-Antworten |

### Environments
- `pcwaechter-prod` (Produktion)
- `pcwaechter-dev` / `pcwaechter-stage` (optional)
- Strikte Trennung von Secrets/URLs/Stripe-Modus je Umgebung.

## Domains / Routing

| Domain | Service | Ziel |
|---|---|---|
| `pcwächter.de` | Website | Static/Next (optional) |
| `home.pcwächter.de` | Kundenportal | Next.js App |
| `console.pcwächter.de` | Admin UI | React SPA |
| `login.pcwächter.de` | Keycloak | Keycloak behind proxy |
| `api.pcwächter.de` | FastAPI | /v1/* |
| `support.pcwächter.de` | Zammad (optional) | Zammad Frontend |

**Proxy-Header Pflicht:** `X-Forwarded-Proto`, `X-Forwarded-For`, `X-Forwarded-Host`

## Technische Anforderungen

### Windows Client
- Min: Win10/11 x64, 2C/4GB RAM
- Empfohlen: 4C/8–16GB

### Server
- Min: 2 vCPU, 4–8GB RAM, SSD
- Empfohlen: 4 vCPU, 8–16GB, SSD, getrennte Volumes für DB/Uploads
