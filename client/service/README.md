# PCWaechter Service

Der Service sammelt lokale Sicherheits- und Stabilitaetsdaten, bewertet Findings und schreibt den Report nach:

- `C:\ProgramData\PCWaechter\agent\scan-report.json`

Legacy-kompatibel wird weiterhin geschrieben:

- `C:\ProgramData\PCWaechter\agent\security-status.json`

## Product UX Intelligence Layer

Zus�tzlich zu den Sensor/Rule-Findings berechnet der Service jetzt:

- Top-3 Priorisierung (`TopFindings`)
- Health Score 0..100 (`HealthScore`)
- Historie pro Finding (`FirstSeenUtc`, `LastSeenUtc`, `ActiveDays`, `ActiveStreakScans`)
- Recently Resolved Liste (`RecentlyResolved`, max 5)
- Device Context (`DeviceContext`: Laptop/Desktop/Server heuristisch)

Die Persistenz liegt in:

- `C:\ProgramData\PCWaechter\service\state.json`

mit tolerantem Laden (alte state-Dateien ohne History crashen nicht).

## Konfiguration

`appsettings.json`:

- `UxIntelligence.ResolvedRecentlyHours` (default 24)
- `UxIntelligence.NewFindingHours` (default 24)
- `UxIntelligence.TopFindingsCount` (default 3)

## Debug-Optionen

- `PCWACHTER_FAKE_CONTEXT=LAPTOP|DESKTOP|SERVER`
- `PCWACHTER_FAKE_FINDINGS=CRITICAL_SECURITY,WARNING_STORAGE`

## Schnelltest

1. Service bauen:
```powershell
dotnet build client/service/AgentService.csproj
```

2. Service starten und Report lesen:
```powershell
dotnet run --project client/service/AgentService.csproj
Get-Content "C:\ProgramData\PCWaechter\agent\scan-report.json"
```

3. Top-3 / Score pr�fen:
- `HealthScore` vorhanden
- `TopFindings` enth�lt max. 3 Items

4. "Gerade behoben" pr�fen:
- Beispiel: Firewall deaktivieren -> Scan
- Firewall wieder aktivieren -> Scan
- Erwartet: Finding verschwindet aus `Findings` und erscheint tempor�r in `RecentlyResolved`
