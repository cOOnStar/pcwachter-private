# OpenAPI Snapshot

This folder contains the latest downloaded OpenAPI specification for the PCWaechter API.

## Files

- `openapi.json`: Current downloaded spec snapshot.
- `update-openapi.ps1`: Script to refresh `openapi.json`.

## Refresh

Run from this directory:

```powershell
.\update-openapi.ps1
```

The script also syncs to `../console/public/openapi.json` when `server/console` exists.

Optional custom source URL:

```powershell
.\update-openapi.ps1 -SourceUrl "https://api.xn--pcwchter-2za.de/openapi.json"
```
