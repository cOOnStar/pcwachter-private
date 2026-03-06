# PC-Waechter Kundenportal - Feature-Dokumentation

> **Version:** 2.1 (nach Konsistenz-Bereinigung)
> **Letzte Aktualisierung:** 2026-03-06
> **Status:** Alle Features vollstaendig implementiert (Frontend mit Mock-Daten)

---

## Seiten-Uebersicht

Das Portal umfasst 11 Seiten:

| Seite | Route | Beschreibung |
|-------|-------|-------------|
| Dashboard | `/` | Uebersicht mit klickbaren Stats |
| Lizenzverwaltung | `/licenses` | Lizenz-Management mit Audit-Log |
| Lizenz kaufen | `/licenses/buy` | Tarifauswahl und Bestellung |
| Meine Geraete | `/devices` | Geraete-Management mit Details |
| Support | `/support` | Ticket-System mit Nachrichtenverlauf |
| Downloads | `/downloads` | GitHub Release-Integration |
| Dokumentation | `/documentation` | Anleitungen und Artikel |
| Benutzerprofil | `/profile` | Konto- und Sicherheitseinstellungen |
| Benachrichtigungen | `/notifications` | Benachrichtigungs-Center |
| Auth Callback | `/auth/callback` | Keycloak Redirect-Handler |
| 404 | `*` | Fehlerseite |

---

## Feature-Details

### 1. Dashboard

- **Klickbare Stats**: 4 Stat-Cards verlinken zur jeweiligen Seite
  - Aktive Lizenzen (berechnet aus Lizenz-Status "Aktiv")
  - Registrierte Geraete (Anzahl aus `devices`-Array, nicht aus Lizenz-Slots)
  - Laeuft bald ab (Lizenzen mit Status "Laeuft bald ab")
  - Offene Tickets (Status "Offen", "In Bearbeitung" und "Warten auf Antwort")
- **Stats-Grid**: `grid-cols-2 lg:grid-cols-4` fuer responsive 2x2 / 4x1 Anzeige
- **Geraete-Trend**: Zeigt verfuegbare Geraete-Plaetze (Summe aller maxDevices)
- **Quick Actions**: Neue Lizenz kaufen, Support kontaktieren, Software herunterladen
- **Letzte Aktivitaeten**: Timeline mit Lizenz-, Ticket- und Update-Ereignissen
- **Systemstatus**: API-Server, Lizenz-Server, Download-Server

### 2. Lizenzverwaltung

- **Stats-Grid**: 4 Kennzahlen
  - Aktive Lizenzen (Status "Aktiv")
  - Laeuft bald ab (Status "Laeuft bald ab")
  - Abgelaufen (Status "Abgelaufen")
  - Belegte Plaetze (Summe aller `devices` aus Lizenzen)
- **Maskierte Schluessel**: Format `XXXX-XXXX-XXXX` (erste 8 Zeichen maskiert, letzte 4 sichtbar)
- **Kopier-Funktion**: One-Click Copy mit Toast-Bestaetigung
- **Audit-Log**: Timeline-Dialog mit allen Aenderungen pro Lizenz
- **Geraete-Nutzung**: Progress-Bar (z.B. "2 von 3 Geraeten genutzt")
- **Status-Badges**: Aktiv (gruen), Laeuft bald ab (gelb), Abgelaufen (rot)
- **Typ-Badges**: Professional (blau), Standard (grau)
- **Suche**: Durchsuchen nach Name oder Schluessel
- **Empty State**: Bei leerer Suche mit Zuruecksetzen-Button
- **Aktionen**: Herunterladen, Verlauf anzeigen, Verlaengern

### 3. Lizenz kaufen

- **2 Tarife**: Standard (4,99 EUR/Monat) und Professional (49,99 EUR/Jahr)
- **Feature-Listen**: Pro Tarif mit Haekchen-Icons
- **Popular-Badge**: Professional als "Beliebteste Wahl" markiert
- **Funktionsvergleich**: Detaillierte Tabelle mit allen Features
- **Trust-Badges**: Datenschutz-konform, 30-Tage-Garantie, Sofortige Aktivierung
- **FAQ-Bereich**: Upgrade, Testversion, Verlaengerung

### 4. Meine Geraete

- **Stats-Grid**: Gesamt, Online, Offline, Wartung
- **Geraete-Karten**: Name, Typ, Betriebssystem
  - 3 Status-Badges: Sicherheitsstatus, Verbindungsstatus, Lizenzstatus
  - Statusmeldung pro Geraet (z.B. "Alles in Ordnung", "Update ausstehend")
- **Quick-Info Grid**: 5 Spalten
  - Letzter Check-in (relative Zeit)
  - Client-Version
  - Letzter Scan (relative Zeit)
  - Letzte Wartung (relative Zeit)
  - Update-Status (aktuell oder verfuegbar)
- **Detailansicht** (Dialog mit 4 Tabs):
  - Allgemein: Geraetename, ID, Typ, OS, Version, Registrierung, Letzter Kontakt
  - Status: Online/Offline, Sicherheitsstatus, Update-Status, Scan, Wartung
  - Lizenz: Zugewiesene Lizenz, Lizenztyp, Gueltigkeit (oder "Keine Lizenz zugewiesen")
  - Verlauf: Chronologische Timeline aller Ereignisse
- **Aktionen**: Details, Umbenennen, Lizenz zuweisen, Entkoppeln
- **Umbenennen-Dialog**: Eigener Dialog mit Eingabefeld
- **Entkoppeln-Dialog**: Bestaetigungsdialog mit Warnung (Lizenz wird freigegeben)
- **Keine IP-Adressen**: Bewusst aus der Anzeige entfernt (Datenschutz)
- **Suche und Filter**: Name/OS suchen, nach Status filtern (Alle, Online, Offline, Wartung)
- **Empty State**: Bei keine Geraete nach Filterung

### 5. Support

- **Stats-Grid**: 4 Kennzahlen
  - Offene Tickets (Status "Offen")
  - In Bearbeitung (Status "In Bearbeitung" + "Warten auf Antwort")
  - Geschlossene Tickets
  - Durchschnittliche Antwortzeit
- **4 Ticket-Status** mit einheitlichen Farben:
  - Offen (gelb)
  - In Bearbeitung (blau)
  - Warten auf Antwort (orange)
  - Geschlossen (gruen)
- **Kategorie-Badges**: Installation (lila), Lizenzierung (indigo), Technischer Support (cyan), Konfiguration (orange)
- **Formatierte Datumsanzeige**: ISO-Timestamps als "TT.MM.JJJJ, HH:MM"
- **Nachrichtenverlauf**: Chat-Bubbles mit Absender-Avatar und Zeitstempel
- **Ticket erstellen**: Dialog mit Betreff, Kategorie (Select), Beschreibung, Dateianhang
- **Ticket schliessen**: Button in Ticket-Liste mit Bestaetigungs-Dialog und Toast
- **Geschlossene Tickets**: Antwortformular durch Hinweis "Dieses Ticket ist geschlossen" ersetzt
- **Statusfilter**: Dropdown mit allen 4 Status + "Alle Status"
- **Suche**: Ticket-Titel und ID durchsuchen
- **Empty State**: Bei leerer Suche/Filter mit Zuruecksetzen-Button

### 6. Downloads

- **GitHub API Integration**: Automatischer Abruf des neuesten Releases
- **Release-Karte**: Tag, Name, Veroeffentlichungsdatum, Anzahl Dateien
- **Release Notes**: Formatierte Anzeige der Release-Beschreibung
- **Asset-Liste**: Alle Download-Dateien mit Dateigroesse und Download-Count
- **Systemanforderungen**: Mindestanforderungen und empfohlene Konfiguration
- **Installationsanleitung**: 4-Schritt-Anleitung (Download, Installation, Lizenz, Fertig)
- **Weitere Ressourcen**: Dokumentation, Changelog, GitHub Repository
- **Loading-State**: Spinner waehrend API-Aufruf
- **Error-State**: Fehlermeldung bei API-Fehler
- **Fallback**: "Keine Releases verfuegbar" bei leerer Response

### 7. Dokumentation

- **Kategorisierte Wissensdatenbank**: Erste Schritte, Konfiguration, Sicherheit, etc.
- **Suchfunktion**: Echtzeit-Suche ueber alle Artikel
- **Beliebte Artikel**: Top-Artikel mit Aufrufzahlen und Sternebewertung
- **Dokumentations-Dateien**: Administratorhandbuch, Schnellstart-Anleitung, API-Dokumentation (PDF)

### 8. Benutzerprofil

- **Avatar**: Initialen-Avatar mit Kamera-Button (Profilbild-Upload)
- **Dynamische Stats**: Aktive Lizenzen und Geraete-Anzahl automatisch berechnet
- **Persoenliche Daten** (Bearbeitbar):
  - E-Mail (Pflichtfeld, ganz oben, rotes `*`, Validierung auf Format und nicht-leer)
  - Vorname, Nachname, Benutzername, Telefon
  - Keycloak User-ID (schreibgeschuetzt, Fingerprint-Icon, Monospace-Schrift)
- **Kein Firma-Feld**: Bewusst entfernt
- **Sicherheit**:
  - Passwort aendern (simulierter Keycloak-Redirect)
  - Zwei-Faktor-Authentifizierung (Toggle)
- **Benachrichtigungs-Einstellungen** (3 Optionen):
  - E-Mail-Benachrichtigungen (standardmaessig aktiv)
  - Lizenz-Erinnerungen (standardmaessig aktiv)
  - Support-Updates (standardmaessig aktiv)
- **Sprache & Region**: Deutsch/English/Francais, Zeitzonen
- **Gefahrenzone**: Account loeschen
  - Warnung bei aktiven Lizenzen (Anzahl + spaetestes Ablaufdatum)
  - "LOESCHEN"-Bestaetigungseingabe (exakter Text erforderlich)
  - Button deaktiviert bis korrekte Eingabe

### 9. Benachrichtigungen

- **Typ-Icons und Farben**:
  - Info (blau, Info-Icon)
  - Warnung (gelb, AlertCircle-Icon)
  - Erfolg (gruen, CheckCircle-Icon)
- **Ungelesen-Markierung**: Blauer Punkt + linker Rand + Shadow bei ungelesenen
- **Badge-Counter**: Anzahl ungelesener Benachrichtigungen im Header
- **Aktionen**:
  - Als gelesen markieren (einzeln pro Benachrichtigung)
  - Alle als gelesen markieren (Batch)
  - Loeschen (einzeln per X-Button)
  - Alle loeschen
- **Lizenz-Ablauf-Format**: Maskiert als `(****-****-E5F6)` mit letzten 4 Zeichen
- **Empty State**: "Keine Benachrichtigungen" bei leerer Liste

### 10. Auth Callback

- Verarbeitet Keycloak-Redirects nach erfolgreicher Anmeldung

### 11. 404-Seite

- Benutzerfreundliche Fehlerseite
- Link zurueck zum Dashboard

---

## UI/UX Features

### Globale Features
- **Breadcrumb-Navigation**: Auf allen Seiten via `Root.tsx`
- **Responsive Design**: `p-4 md:p-6` Paddings, responsive Ueberschriften
- **Stats-Grid**: Konsistent `grid-cols-2 lg:grid-cols-4` auf Dashboard, Lizenzen, Support, Geraete
- **Motion-Animationen**: Einblend-, Slide- und Scale-Animationen
- **Toast-Benachrichtigungen**: Sonner fuer Feedback bei allen Aktionen
- **Empty States**: Bei leerer Suche/Filter auf Support-, Lizenz- und Geraete-Seite

### Konsistenz-Regeln
- **Einheitliche Terminologie**: Lizenz, Geraet, Konto, Benachrichtigung, Ticket
- **Einheitliches Status-System**: Konsistente Farben und Icons ueber alle Seiten
- **Einheitliches Datumsformat**: ISO-Timestamps intern, "TT.MM.JJJJ, HH:MM" in der Anzeige
- **Konsistente Aktionen**: Umbenennen, Entkoppeln (nicht "Trennen"), Verlaengern, Schliessen

### Technische Features
- **React.forwardRef**: Button und DialogOverlay fuer React 18 / Radix UI Kompatibilitaet
- **React Query**: Server-State-Management mit Caching
- **IndexedDB**: Offline-Datenspeicherung
- **Skeleton-Screens**: Loading-States
- **Global Loading Indicator**: Top-Progress-Bar

---

## Status-System (einheitlich)

### Lizenz-Status
| Status | Farbe | Badge-Klassen |
|--------|-------|--------------|
| Aktiv | Gruen | `bg-green-100 text-green-700 border-green-300` |
| Laeuft bald ab | Gelb | `bg-yellow-100 text-yellow-700 border-yellow-300` |
| Abgelaufen | Rot | `bg-red-100 text-red-700 border-red-300` |

### Geraete-Status
| Status | Farbe | Badge-Klassen |
|--------|-------|--------------|
| Online | Gruen | `bg-green-100 text-green-700 border-green-300` |
| Offline | Rot | `bg-red-100 text-red-700 border-red-300` |
| Wartung | Gelb | `bg-yellow-100 text-yellow-700 border-yellow-300` |

### Sicherheits-Status
| Status | Farbe | Badge-Klassen |
|--------|-------|--------------|
| Geschuetzt | Gruen | `bg-green-100 text-green-700 border-green-300` |
| Warnung | Gelb | `bg-yellow-100 text-yellow-700 border-yellow-300` |
| Kritisch | Rot | `bg-red-100 text-red-700 border-red-300` |

### Ticket-Status
| Status | Farbe | Badge-Klassen |
|--------|-------|--------------|
| Offen | Gelb | `bg-yellow-100 text-yellow-700 border-yellow-300` |
| In Bearbeitung | Blau | `bg-blue-100 text-blue-700 border-blue-300` |
| Warten auf Antwort | Orange | `bg-orange-100 text-orange-700 border-orange-300` |
| Geschlossen | Gruen | `bg-green-100 text-green-700 border-green-300` |

### Kategorie-Badges (Support)
| Kategorie | Farbe | Badge-Klassen |
|-----------|-------|--------------|
| Installation | Lila | `bg-purple-100 text-purple-700 border-purple-300` |
| Lizenzierung | Indigo | `bg-indigo-100 text-indigo-700 border-indigo-300` |
| Technischer Support | Cyan | `bg-cyan-100 text-cyan-700 border-cyan-300` |
| Konfiguration | Orange | `bg-orange-100 text-orange-700 border-orange-300` |

---

## Mock-Daten (aktueller Stand)

### Lizenzen (4 Stueck)
| ID | Name | Typ | Status | Geraete | Max |
|----|------|-----|--------|---------|-----|
| 1 | PC-Waechter Professional | Professional | Aktiv | 2 | 3 |
| 2 | PC-Waechter Standard | Standard | Aktiv | 2 | 3 |
| 3 | PC-Waechter Professional | Professional | Laeuft bald ab | 0 | 1 |
| 4 | PC-Waechter Standard | Standard | Abgelaufen | 0 | 1 |

### Geraete (5 Stueck)
| ID | Name | Typ | Status | Sicherheit | Lizenz-ID |
|----|------|-----|--------|------------|-----------|
| dev-1 | Calvin-PC | Desktop | Online | Geschuetzt | 1 |
| dev-2 | Laptop-Wohnzimmer | Laptop | Offline | Warnung | 2 |
| dev-3 | SERVER-DC01 | Server | Online | Geschuetzt | 1 |
| dev-4 | Buero-Laptop | Laptop | Offline | Kritisch | - (keine) |
| dev-5 | Arbeits-Desktop | Desktop | Wartung | Warnung | 2 |

### Tickets (5 Stueck)
| ID | Titel | Status | Kategorie |
|----|-------|--------|-----------|
| 1 | Installation schlaegt fehl | Geschlossen | Installation |
| 2 | Frage zur Lizenzaktivierung | Offen | Lizenzierung |
| 3 | Performance-Probleme nach Update | In Bearbeitung | Technischer Support |
| 4 | Backup-Funktion nicht verfuegbar | Warten auf Antwort | Technischer Support |
| 5 | Netzwerkfreigabe konfigurieren | Geschlossen | Konfiguration |

### Benachrichtigungen (3 Stueck)
| ID | Titel | Typ | Gelesen |
|----|-------|-----|---------|
| 1 | Neue Version verfuegbar | info | Nein |
| 2 | Lizenz laeuft bald ab | warning | Nein |
| 3 | Support-Ticket aktualisiert | success | Ja |

---

## Technologie-Stack

### Core
- React 18 + TypeScript
- React Router 7 (Data Mode)
- Tailwind CSS v4
- Vite

### UI
- Radix UI Primitives
- Lucide React Icons
- Motion (Animationen)
- Sonner (Toasts)

### State & Data
- TanStack React Query v5
- IndexedDB (Offline-Cache)
- Keycloak JS (Authentifizierung)

---

## Dateistruktur

```
/src/
  app/
    components/
      auth/ProtectedRoute.tsx
      layout/Header.tsx, ProtectedLayout.tsx, Root.tsx, Sidebar.tsx
      ui/breadcrumbs.tsx, button.tsx, card.tsx, dialog.tsx, ...
    data/mock-data.ts
    pages/
      AuthCallback.tsx, BuyLicense.tsx, Dashboard.tsx, Devices.tsx,
      Documentation.tsx, Downloads.tsx, Licenses.tsx, NotFound.tsx,
      Notifications.tsx, Profile.tsx, Support.tsx
    providers/QueryProvider.tsx
    routes.tsx
  contexts/AuthContext.tsx
  hooks/
    index.ts, useAuth.ts, useGitHubRelease.ts, useLicenses.ts,
    useOptimisticUpdate.ts, useSupportTickets.ts
  lib/indexedDB.ts, keycloak.ts
  styles/fonts.css, index.css, tailwind.css, theme.css
  types/index.ts
```

---

## Anmerkungen

- Alle Features sind mit Mock-Daten implementiert
- Mock-Daten sind konsistent: Lizenz-Geraete-Zuweisungen, Zaehler und Status passen zusammen
- Bei Deployment mit echtem Backend muessen die API-Calls implementiert werden
- Die Keycloak-Integration bleibt unveraendert
- Alle Komponenten sind TypeScript-typisiert
- Kein Firma-Feld im Profil, keine IP-Adressen bei Geraeten, kein Newsletter in den Profil-Einstellungen
