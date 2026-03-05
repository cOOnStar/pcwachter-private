# PCWächter Monorepo

Dieses Repository ist in Client- und Server-Teile gegliedert.

## Struktur

### Client
- GUI (WPF): `client/gui`
- Service (Windows-Dienst): `client/service`
- Agent API (Sender): `client/api`
- Installer: `client/installer`

### Server
- API (FastAPI): `server/api`
- Admin Console (React): `server/console`
- Kundenportal (Next.js): `server/home`
- Keycloak (Identity): `server/keycloak`
- Infra (Compose): `server/infra/compose`

## Prozessmodell (Task-Manager)

- `PC Waechter` (Desktop-App / Dashboard)
- `PCWaechter.Service.exe` (sammelt PC-Daten, stellt lokal bereit)
- `PCWaechter.Updater.exe` (Update-Mechanismus)
- `PCWaechter.API.exe` (sendet Daten an Server-API)

## Schnellstart

Aktueller Client-Stand: `0.0.7.2`

### Server (alle Services)
```bash
cp .env.example .env          # einmalig – Secrets eintragen
make up                        # Postgres + Keycloak + API + Home + Console
make migrate                   # DB Migrationen ausführen
make keycloak-setup KEYCLOAK_ADMIN_PASSWORD=... # Keycloak einrichten
```

Hinweis zur Versionierung:
- Client-Release (z. B. `0.0.73`) ist getrennt von Server-Containern.
- Server nutzt pro Service eigene Image-Tags (`PCW_API_IMAGE`, `PCW_CONSOLE_IMAGE`, `PCW_HOME_IMAGE`) in `.env`.

### API (lokal)
```bash
cd server/api
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

### Console (lokal)
```bash
cd server/console
npm ci && npm run dev
```

### Client
```bash
dotnet build client/pcwaechter-client.sln -c Release
```

## Home-Portal ENV-Variablen (`server/home/.env.local`)

| Variable | Pflicht | Beschreibung |
|---|---|---|
| `NEXTAUTH_URL` | ✓ | Öffentliche URL des Portals (z. B. `https://home.pcwaechter.de`) |
| `NEXTAUTH_SECRET` | ✓ | Zufälliger Secret-String (mind. 32 Zeichen) |
| `KEYCLOAK_ISSUER` | ✓ | Keycloak-Realm-URL (z. B. `https://auth.pcwaechter.de/realms/pcw`) |
| `KEYCLOAK_CLIENT_ID` | ✓ | OAuth-Client-ID in Keycloak |
| `KEYCLOAK_CLIENT_SECRET` | ✓ | OAuth-Client-Secret aus Keycloak |
| `NEXT_PUBLIC_API_URL` | ✓ | Öffentliche API-URL (z. B. `https://api.pcwächter.de`) |
| `API_INTERNAL_URL` | – | Interne API-URL für Server-Side-Requests (z. B. `http://pcw-api:8000/api/v1`); fällt auf `NEXT_PUBLIC_API_URL` zurück |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | – | Stripe Publishable Key; fehlt → Billing zeigt direkt Plan-Auswahl |
| `STRIPE_SECRET_KEY` | – | Stripe Secret Key (für Checkout & Kundenportal) |
| `STRIPE_WEBHOOK_SECRET` | – | Stripe Webhook-Signing-Secret |
| `NEXT_PUBLIC_RELEASE_BASE_URL` | – | GitHub-Releases-Download-URL; Standard: `https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download` |
