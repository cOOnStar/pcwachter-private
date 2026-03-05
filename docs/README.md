# Dokumentations-Übersicht

Vollständige Inhaltsübersicht: `docs/TOC.md`

## Technische Dokumentation

- Übersicht: `docs/00-overview.md`
- Architektur: `docs/01-architecture.md`
- Keycloak (Realm Export): `docs/02-keycloak.md`
- ENV Variablen: `docs/03-env.md`
- Docker / Infra: `docs/04-docker.md`
- API Endpoints: `docs/05-api.md`
- Datenbank / Migrations: `docs/06-db.md`
- Frontends: `docs/07-frontends.md`
- Release / Updates: `docs/08-release-updates.md`
- Backend Settings: `docs/09-backend-settings.md`

## Deployment & Setup

- Deploy Guide: `docs/deploy.md`
- Keycloak Setup: `docs/keycloak-setup.md`
- Services Reference: `docs/services.md`

## Release & Versionierung

- Release-Prozess: `docs/07_release/00_release-process.md`
- Versionierung: `docs/07_release/01_versioning.md`
- Release Notes: `docs/releases/`

## Pflege-Regeln

- Technische Docs (ENV, API, DB) werden via `scripts/generate_docs.py` generiert.
- Release-Notes nur unter `docs/releases/<programm>/` ablegen.
- Deploy- und Setup-Guides unter `docs/deploy.md` / `docs/keycloak-setup.md` pflegen.
