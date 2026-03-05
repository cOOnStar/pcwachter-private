# PCWächter – Tech Docs Generator (from scratch)

Du wolltest eine technische Dokumentation, die wirklich *alle* Details enthält:
- jede ENV Variable / Args (Client/Server)
- alle Next.js/Vite Configs
- alle DB-Spalten (Postgres)
- alle FastAPI Endpoints (real aus Code)
- alle Keycloak Toggles/Client-Settings (real aus Realm Export)
- plus strukturierte Docs als Download

⚠️ Wichtig: Dafür brauche ich die echten Quellen:
1) FastAPI Backend Repo (ZIP)
2) Server/Infra (docker-compose/helm/whatever) (ZIP)
3) Keycloak Realm Export JSON (Admin → Realm Settings → Export)
4) Optional: DB Connection String oder SQL Dump (für 100% Spalten/Indizes)
5) Optional: Home/Console Repos (ZIP)

Da Console.zip und Kundenportal.zip nur Beispiele waren, enthält dieses Paket:
- eine vollständige docs/ Struktur (Templates)
- Generator-Skripte, die du in deinem Repo ausführst
- die erzeugen dann automatisch Markdown-Dokumente mit *allen* gefundenen Details

## Quick Start

1) Lege deine Repos in ein Arbeitsverzeichnis, z.B.:
   - ./pcwaechter/server/apps/api
   - ./pcwaechter/server/infra/compose
   - ./pcwaechter/server/apps/home
   - ./pcwaechter/server/apps/console

2) Keycloak Export:
   - exportiere Realm als JSON -> ./inputs/keycloak-realm.json

3) (Optional) DB Schema Dump:
   - `pg_dump --schema-only ... > ./inputs/schema.sql`

4) Generator laufen lassen:
   - `python scripts/generate_docs.py --root ./pcwaechter --out ./generated-docs --keycloak ./inputs/keycloak-realm.json --schema ./inputs/schema.sql`

5) Ergebnis:
   - ./generated-docs/ enthält Markdown-Dokumente + TOC

## Output (was erzeugt wird)
- 00-overview.md
- 01-architecture.md
- 02-keycloak.md (Clients, Redirects, WebOrigins, Flows, Toggles, Mappers)
- 03-env.md (alle ENV Vars + Fundstellen)
- 04-docker.md (Services, Ports, Volumes, Env, Networks)
- 05-api.md (alle FastAPI Router/Endpoints + Methods + Paths)
- 06-db.md (Tabellen, Spalten, Indizes, Constraints)
- 07-frontends.md (Next.js/Vite Config, Routes, API calls)
- 08-release-updates.md (Channels, Manifest, Rollouts)
