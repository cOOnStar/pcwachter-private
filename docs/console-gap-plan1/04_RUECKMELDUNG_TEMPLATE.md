# RÜCKMELDUNG (console-gap-plan1)

Kontext
Repo: pcwachter-private
Branch: <name>
Commit (nach Änderungen): <full sha>

P1-1 Activity Feed UI
Status (PASS/FAIL): < >
Nachweis:
- File(s): <...>
- Route: <...>
- Screenshot/Console-Log: <ok/fail>

P1-2 Knowledge Base UI (read-only)
Status (PASS/FAIL): < >
Nachweis:
- File(s): <...>
- API call works: <yes/no> (HTTP code + short output)

P1-3 Support Overview UI
Status (PASS/FAIL): < >
Nachweis:
- list tickets (user): <ok/fail>
- list tickets (admin all=true): <ok/fail>
- reply: <ok/fail>
- attachments: <ok/fail>
Fehler (falls FAIL): <paste stacktrace + request>

P1-4 Device Update Channel override (API + UI)
Status (PASS/FAIL): < >
Nachweis:
- API: PATCH /console/ui/devices/{id}/update-channel -> HTTP <...>
- UI: dropdown visible + saves + persists after reload: <yes/no>

P2 optional (falls gemacht)
UpdatesPage (Manifest Viewer): PASS/FAIL
ClientConfig: PASS/FAIL
KB CRUD: PASS/FAIL
Rules UI: PASS/FAIL

git diff --stat:
<paste>

Tests
npm run build: PASS/FAIL
API smoke curls: PASS/FAIL (paste outputs)
