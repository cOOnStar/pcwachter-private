# RÜCKMELDUNG (audits_fix)

> Bitte exakt so ausfüllen und hier posten.  
> Wenn etwas FAIL ist: **Fehlertext + Command** mitliefern.

## Kontext
Repo: pcwachter-private  
Branch: <name>  
Commit (nach Änderungen): <hash oder 'none'>  

## P0-1 Agent Register Legacy default OFF
Status (PASS/FAIL):  
Nachweis:
- Dateiänderungen: (settings.py + security.py)  
- Curl:
  - no bootstrap + legacy false → HTTP ___ detail ___
  - bootstrap set (wrong) → HTTP ___
  - bootstrap set (ok) → HTTP ___

## P0-2 Greenfield DB Init
Status (PASS/FAIL):  
Variante umgesetzt (A/B):  
Commands:
- <cmd1>
- <cmd2>
Result:
- fresh DB init ok: yes/no
- existing DB no regression: yes/no
Logs / Error (falls FAIL):

## P0-3 Release Variante A
Status (PASS/FAIL/PARTIAL):  
Workflow file(s):  
Release Assets:
- offline name ok: yes/no
- live name ok: yes/no
- manifest ok: yes/no
Notes / unknowns:

## P1-4 Support Reply + Attachments
Status (PASS/FAIL/PARTIAL):  
Endpoints:
- reply: ok yes/no
- attachments: ok yes/no
curl outputs (short):
- reply:
- upload:

## P1-5 Notifications persistent
Status (PASS/FAIL/PARTIAL):  
Migration created: yes/no (filename)  
Endpoints ok: yes/no  
curl outputs (short):

## P2 optional
Welche umgesetzt:
- client_config: yes/no
- downloads/kb: yes/no
- ops keycloak: yes/no
- rules engine: yes/no / unknown

## git diff / summary
- `git diff --stat`:
<einfügen>

## Fehler (falls vorhanden)
<copy/paste>
