# Task P1-4 — Support Reply + Attachments

## Ziel
- Self‑Service (pcw_user) kann:
  - Ticket erstellen (besteht)
  - Ticket listen (scoped) (besteht)
  - Ticket detail (ownership) (besteht)
  - **neu:** Reply posten
  - **neu:** Attachment Upload (multipart) → in Zammad payload übersetzen

## Zammad API Referenz
- Ticket Articles „Create“: `POST /api/v1/ticket_articles` (supports attachments). (siehe Zammad Docs)
- Download attachments: `GET /api/v1/ticket_attachment/{ticket}/{article}/{attachment}`. (siehe Zammad Docs)

## Umsetzung (Codex)
1. `server/api/app/routers/support.py`
   - Add:
     - `POST /tickets/{ticket_id}/reply`
       - Body: `{"body": "...", "content_type": "text/plain|text/html", "internal": false, "attachments": [..]}`
       - Call Zammad:
         - `POST <ZAMMAD_BASE_URL>/api/v1/ticket_articles`
         - Payload minimal:
           - `ticket_id`, `body`, `content_type`, `type` = `"note"` (oder `"email"` falls gewünscht), `internal`
           - optional `attachments`:
             [
               {
                 "filename": "portal.txt",
                 "data": "<base64>",
                 "mime-type": "text/plain"
               }
             ]
   - Add:
     - `POST /attachments`
       - `multipart/form-data` mit `file`
       - Response: `{"filename":..,"data":..,"mime-type":..,"size":..}`
       - Zweck: UI kann diesen JSON‑Block direkt in Reply/Create wiederverwenden.
   - Reuse existing 502 mapping for upstream errors.

2. (Optional) `POST /tickets` erweitern um `attachments` (gleiches Schema), damit Create ebenfalls Attachments unterstützt.

## Akzeptanz / Smoke
- Reply ohne attachment → 201 von Zammad, PCW gibt JSON zurück
- Reply mit attachment (kleines txt) → 201, attachments[] im response
- Upload endpoint:
  - rejects > max size (config) → 413
  - returns base64 JSON for small file

## Rückmeldung
- curl Beispiel für Reply
- curl Beispiel für Upload
