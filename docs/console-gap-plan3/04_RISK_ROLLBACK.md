# 04 — Risiken & Rollback

## Risiken
1) **Zammad nicht erreichbar / falsch proxied** → Support UI bleibt im ErrorBanner/Hints.
2) **Keycloak Token ohne email** → Support endpoints liefern 400 `user_email_missing`.
3) **ZAMMAD_DEFAULT_GROUP_ID falsch** → Ticket create kann 502 upstream error liefern.
4) **Uploads zu groß** → Proxy upload limit / API limit blockiert attachments.

## Rollback
- Code-Änderungen Console UI: `git revert <commit>` oder `git checkout` auf vorherigen Stand.
- ENV Änderungen: Variablen entfernen → Support geht wieder in “not configured”.
- Zammad Compose Service: `docker compose ... stop zammad` (wenn als Service).
- Keine DB Rollbacks nötig, sofern nur UI/ENV/Proxy Änderungen.

## Safe-Guards
- API behandelt httpx RequestError bereits als 502 (zammad_unreachable).
- Diag Endpoints sind admin-only: kein leak an normale User.
