# PCWaechter Console

OpenAPI-based admin frontend for PCWaechter.

## Features

- Loads OpenAPI from API and lists all `/console/ui/*` endpoints.
- Lets admins execute requests with path/query/body input.
- Shows response data, new fields, and missing fields compared to OpenAPI schema.

## Local Development

```powershell
cd server\console
npm install
npm run sync:openapi
npm run dev
```

Open:

- `http://localhost:5173`

Default API URL:

- `http://localhost:18080` (override with `VITE_API_BASE_URL`)

## Docker

```powershell
cd server\console
docker compose up --build -d
```

Open:

- `http://localhost:13001`

## OpenAPI Snapshot

`public/openapi.json` can be synced from `server/openapi/openapi.json`:

```powershell
npm run sync:openapi
```
