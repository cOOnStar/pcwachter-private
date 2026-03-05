# Definition of Done (prüfbar)

## P0 – Muss
- [ ] **Greenfield**: Auf leerer DB kann ich reproduzierbar starten:  
  `bootstrap.sql` → `alembic upgrade head` → API startet ohne Fehler.  
  Nachweis Lücke im IST: `audit_05.03.2026/06-migrations.md:L38-L40`
- [ ] **Kein statischer API-Key mehr im Agent-Register Flow** (oder Legacy nur hinter Toggle)  
  Nachweis IST Problem: `audit_05.03.2026/03-router-dependencies.md:L25`
- [ ] **GitHub Release Variante A**: Latest Release enthält  
  `PCWaechter-Offline-Setup.exe`, `PCWaechter-Live-Installer.exe`, `installer-manifest.json`  
- [ ] Live Installer lädt Offline Installer + verifiziert `sha256`

## P1 – Zielbild v6.3 Kern
- [ ] DB: `devices.desktop_version`, `devices.updater_version`, `devices.update_channel` vorhanden
- [ ] API: `POST /api/v1/client/status` vorhanden und schreibt Device Versions
- [ ] Console: Device Detail/List zeigt Desktop/Updater Version + Channel
- [ ] Home: `/download` zeigt stabile GitHub Download Links

## P1 – Support (wenn aktiviert)
- [ ] Backend: `/api/v1/support/*` endpoints vorhanden (mind. list/create/reply/upload)
- [ ] Home: Support UI erreichbar oder Doku ist bereinigt (keine „toten“ Links)

## P2 – Nice-to-have
- [ ] Notifications persistent + Read-State
- [ ] Keycloak: Legacy Clients entfernt oder begründet dokumentiert
- [ ] Realm Export JSON versioniert

