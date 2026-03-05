# 05 — Definition of Done (Plan3)

## P0 — Support/Zammad produktiv
- [ ] `ZAMMAD_BASE_URL` + `ZAMMAD_API_TOKEN` gesetzt und API gestartet.
- [ ] `GET /api/v1/support/admin/diag/zammad-roles` liefert Rollenliste.
- [ ] Normaler User kann Ticket erstellen (`POST /api/v1/support/tickets`) → 200/201.
- [ ] Normaler User listet Tickets und sieht nur eigene.
- [ ] Attachments Upload funktioniert (falls aktiviert).

## P1 — Console UX
- [ ] SupportPage zeigt **Hintbox** statt generischem ErrorBanner bei `support_not_configured` / `zammad_unreachable`.
- [ ] Admin sieht Button “Diag: Zammad Roles” und Ergebnis wird im UI angezeigt.
- [ ] `npm run build` grün.

## P1 — Home Support (falls umgesetzt)
- [ ] Home User kann Support Tickets erstellen/listen/reply/upload.

## P2 — Backlog dokumentiert
- [ ] UpdatesPage/ClientConfig/KB CRUD/Rules UI als Tickets/Issues oder Plan4 dokumentiert.
