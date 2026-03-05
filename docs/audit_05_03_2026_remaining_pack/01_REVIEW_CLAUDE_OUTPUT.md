# Review der geposteten Claude-Ausgabe

## Was gut ist
- P0/P1 Punkte wurden strukturiert abgearbeitet.
- Es gibt ein DoD-Table mit PARTIAL Markierungen.
- Es werden neue ENV Keys genannt (Bootstrap Key, Zammad, Release Base URL).

## Was formal noch nicht sauber ist (gemäß deinen Regeln)
1) **Nachweise**: "Datei:Zeile" wird teils genannt, teils nicht.  
   - Für echte Auditierbarkeit: bitte in einem finalen Report überall **konkret**: `path:line-line` oder `command + output snippet`.
2) **Idempotenz**: Bootstrap SQL ist ok, aber nur dann, wenn Alembic niemals dieselben Tabellen nochmals erstellt.  
   - Abnahme: `alembic upgrade head` auf frisch gebootstraptem DB muss ohne "already exists" laufen.
3) **Updates v6.3 (Variante A)**: Ist noch **PARTIAL**
   - Public Release Repo Workflow nur als Template abgelegt
   - Live-Installer Binary/Code ist unknown
4) **Support/Zammad**: customer_id/group mapping TODO (Hardcode ist riskant)

## Empfohlene Abnahme-Checks (lokal)
- OpenAPI zeigt neue Endpoints:
  - `/v1/client/status`
  - `/v1/support/*` (wenn env gesetzt)
- DB Schema:
  - `devices.desktop_version`, `devices.updater_version`, `devices.update_channel` vorhanden
- Agent Register:
  - funktioniert mit Bootstrap-Key, Legacy kann per Toggle aus
- Home /download:
  - zeigt stabile GitHub latest/download Links
- GitHub Release:
  - latest Release enthält manifest + offline + live Assets mit stabilen Namen
