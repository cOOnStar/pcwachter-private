# PCWachter Desktop (WPF)

Die Desktop-App ist eine reine UI-Schicht. Alle Scans, Regeln und Fixes laufen im Service. Die App zeigt den aktuellen `ScanReportDto` an und triggert Aktionen per Named Pipe.

## Voraussetzungen

- Windows
- .NET 10 SDK
- Laufender Service mit IPC Pipe (`PCWaechter.Service.Ipc`, Fallback `PCWachterPipe`)

## Projektstruktur

- `Themes/Theme.xaml`: Dark Theme + Card/Nav/Button Styles
- `Views/Pages/*`: Seiten (Dashboard, Sicherheit, Windows, Updates, Speicher, Netzwerk, Verlauf, Konto, Optionen, Hilfe)
- `Views/Controls/*`: Reusable Controls (`FindingCardControl`, `StatCardControl`, `SectionHeaderControl`, `ProgressOverlay`)
- `ViewModels/*`: MVVM (Navigation + Seitenlogik)
- `Services/IpcClientService*`: Named Pipe Client + Request/Response Handling
- `Services/ReportStore.cs`: zentraler Report-Store für alle Seiten

## Startablauf

1. App startet und erstellt `MainViewModel`
2. IPC Connect (`ConnectAsync`)
3. `GetLatestReportAsync`
4. `SubscribeEventsAsync` (Polling/Refresh)
5. `TriggerScanAsync`

Wenn der Service nicht erreichbar ist:

- globaler Warnbanner oben links in der Content-Fläche
- Button `Erneut verbinden`
- UI bleibt bedienbar

## Navigation

Rechte Navigation (fixe Reihenfolge):

1. Dashboard
2. Sicherheit
3. Windows
4. Windows Updates
5. Speicher
6. Netzwerk
7. Verlauf / Historie
8. PCWächter Konto
9. Optionen
10. Hilfe

## Aktionen

- `RunRemediation`: wird über IPC an den Service gesendet
- `OpenExternal`: wird lokal im Desktop ausgeführt
- `OpenDetails`: Detaildialog im Desktop

Zusätzlich pro Finding:

- Snooze (7 Tage)
- Ignorieren

## Build

```powershell
dotnet build client/gui/PCWächter.csproj
```

## Hinweise für Tests

- Service stoppen -> Banner muss sichtbar sein
- Service starten -> `Erneut verbinden` klicken -> Banner verschwindet
- `Jetzt scannen` im Dashboard auslösen
- In `Optionen` Policy ändern und speichern
- In `Hilfe` Logs öffnen und Service-Status prüfen
