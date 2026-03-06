# PC-Waechter Kundenportal

Ein modernes Kundenportal fuer PC-Waechter Kunden mit Keycloak-Authentifizierung.

**Version:** 2.1 (nach Konsistenz-Bereinigung)
**Letzte Aktualisierung:** 2026-03-06

## Features

- **Keycloak Integration**: Sichere Authentifizierung ueber login.pcwaechter.de
- **Dashboard**: Uebersicht ueber Lizenzen, Geraete, ablaufende Lizenzen und offene Tickets mit klickbaren Stats
- **Lizenzverwaltung**: Verwaltung aller PC-Waechter Lizenzen mit Audit-Log, maskierten Schluesseln (XXXX-XXXX-XXXX) und Geraete-Nutzungs-Progressbar
- **Lizenz kaufen**: Tarifauswahl (Standard/Professional) mit Funktionsvergleich
- **Meine Geraete**: Registrierte Geraete mit Status, Sicherheits-Check, Lizenz-Zuordnung, Verlauf und Aktionen
- **Support-System**: Ticket-System mit Kategorie-Badges, Nachrichtenverlauf, Ticket-Schliessen-Funktion und 4 Ticket-Status
- **Downloads**: Automatischer Download-Bereich mit GitHub API Integration
- **Dokumentation**: Kategorisierte Anleitungen, beliebte Artikel und Suchfunktion
- **Benutzerprofil**: Kontoinformationen, Keycloak-ID (schreibgeschuetzt), Account-Loeschung mit Lizenz-Warnung
- **Benachrichtigungen**: Benachrichtigungs-Center mit Typ-Icons und Batch-Aktionen
- **Breadcrumb-Navigation**: Globale Breadcrumbs auf allen Seiten
- **404-Seite**: Benutzerfreundliche Fehlerseite

## Keycloak Konfiguration

Das Portal nutzt Keycloak fuer die Authentifizierung. Die Konfiguration erfolgt ueber Umgebungsvariablen.

### Umgebungsvariablen

```env
# Keycloak Configuration
VITE_KEYCLOAK_URL=https://login.xn--pcwchter-2za.de
VITE_KEYCLOAK_REALM=pcwaechter-prod
VITE_KEYCLOAK_CLIENT_ID=home-web
```

### Keycloak Client Konfiguration

Der Keycloak Client muss wie folgt konfiguriert sein:

- **Client ID**: `home-web`
- **Client Type**: Public
- **Valid Redirect URIs**:
  - `https://home.xn--pcwchter-2za.de/auth/callback`
  - `https://home.xn--pcwchter-2za.de/*`
- **Valid Post Logout Redirect URIs**:
  - `https://home.xn--pcwchter-2za.de/`
- **Web Origins**: `https://home.xn--pcwchter-2za.de`

### Authentifizierungs-Flow

1. Benutzer greift auf `home.pcwaechter.de` zu
2. Portal prueft Authentifizierung ueber Keycloak
3. Falls nicht authentifiziert: Redirect zu `login.pcwaechter.de`
4. Nach erfolgreicher Anmeldung: Redirect zurueck zum Portal
5. Portal laedt Benutzerdaten aus Keycloak

### Benutzerdaten aus Keycloak

**Standard Claims**:
- `sub` (User ID)
- `preferred_username`
- `email`
- `email_verified`
- `given_name` (Vorname)
- `family_name` (Nachname)

**Custom Attributes** (optional):
- `license_tier`: Lizenz-Stufe des Kunden
- `license_roles`: Spezielle Lizenz-Rollen

## Entwicklung

### Installation

```bash
pnpm install
```

### Development Server

```bash
pnpm dev
```

### Build

```bash
pnpm build
```

### Lokale Entwicklung ohne Keycloak

Fuer die lokale Entwicklung verwendet das Portal automatisch einen "check-sso" Modus, der eine bestehende Keycloak-Session prueft, aber bei fehlender Authentifizierung nicht automatisch zum Login umleitet.

Um mit einem echten Keycloak-Server zu testen:
1. `.env` Datei mit gueltigen Keycloak-Daten erstellen
2. Sicherstellen, dass die Redirect URIs im Keycloak Client auch `http://localhost:5173/*` enthalten
3. Development Server starten

## Seiten im Detail

### Dashboard (`/`)
- 4 klickbare Stat-Cards: Aktive Lizenzen, Registrierte Geraete, Laueft bald ab, Offene Tickets
- Geraete-Zaehlung basiert auf tatsaechlich registrierten Geraeten (nicht Lizenz-Slots)
- Offene Tickets zaehlen auch "Warten auf Antwort"-Status mit
- Quick Actions: Neue Lizenz kaufen, Support kontaktieren, Software herunterladen
- Letzte Aktivitaeten (Timeline)
- Systemstatus (API-Server, Lizenz-Server, Download-Server)

### Lizenzverwaltung (`/licenses`)
- Stats: Aktive Lizenzen, Laeuft bald ab, Abgelaufen, Belegte Plaetze
- Maskierte Lizenzschluessel (XXXX-XXXX-XXXX) mit Kopier-Funktion
- Lizenz-Audit-Log (Timeline-Dialog)
- Geraete-Nutzung mit Progress-Bar (z.B. "2 von 3 Geraeten genutzt")
- Status-Badges: Aktiv (gruen), Laeuft bald ab (gelb), Abgelaufen (rot)
- Typ-Badges: Professional (blau), Standard (grau)
- Suche und Empty State

### Lizenz kaufen (`/licenses/buy`)
- Standard (4,99 EUR/Monat) und Professional (49,99 EUR/Jahr)
- Detaillierter Funktionsvergleich (Tabelle)
- Trust-Badges und FAQ-Bereich

### Meine Geraete (`/devices`)
- Stats: Gesamt, Online, Offline, Wartung
- Geraete-Karten mit Status, Sicherheitsstatus, Lizenzstatus
- Statusmeldung pro Geraet (z.B. "Alles in Ordnung", "Update ausstehend")
- Quick-Info Grid: Letzter Check-in, Client-Version, Letzter Scan, Letzte Wartung, Update-Status
- Detailansicht (Dialog mit 4 Tabs: Allgemein, Status, Lizenz, Verlauf)
- Aktionen: Details, Umbenennen, Lizenz zuweisen, Entkoppeln
- Keine IP-Adressen in der Anzeige (Datenschutz)

### Support (`/support`)
- Stats: Offene Tickets, In Bearbeitung (inkl. "Warten auf Antwort"), Geschlossen, Durchschnittliche Antwortzeit
- 4 Ticket-Status: Offen (gelb), In Bearbeitung (blau), Warten auf Antwort (orange), Geschlossen (gruen)
- Kategorie-Badges: Installation (lila), Lizenzierung (indigo), Technischer Support (cyan), Konfiguration (orange)
- Vollstaendiger Nachrichtenverlauf mit Chat-Bubbles
- Ticket erstellen mit Kategorien und Dateianhang
- Ticket schliessen mit Bestaetigungs-Dialog
- Geschlossene Tickets: Antwortformular durch Hinweis ersetzt
- Suche und 4-fach Statusfilter (inkl. "Warten auf Antwort")

### Downloads (`/downloads`)
- Automatische GitHub Release-Integration (neuestes Release)
- Release Notes, Asset-Liste mit Dateigroesse und Download-Count
- Systemanforderungen und 4-Schritt-Installationsanleitung
- Weitere Ressourcen: Dokumentation, Changelog, GitHub

### Dokumentation (`/documentation`)
- Kategorisierte Wissensdatenbank mit Suchfunktion
- Beliebte Artikel mit Aufrufzahlen und Bewertungen
- Dokumentations-Dateien (PDF-Handbuecher)

### Benutzerprofil (`/profile`)
- Avatar mit Initialen und Kamera-Button
- Dynamische Stats (Aktive Lizenzen und Geraete aus Mock-Daten berechnet)
- Persoenliche Daten: E-Mail (Pflichtfeld mit Validierung, ganz oben), Vorname, Nachname, Benutzername, Telefon
- Keycloak User-ID (schreibgeschuetzt, Fingerprint-Icon, Monospace)
- Kein Firma-Feld
- Sicherheit: Passwort aendern (Keycloak-Redirect), 2FA-Toggle
- Benachrichtigungen: E-Mail-Benachrichtigungen, Lizenz-Erinnerungen, Support-Updates (kein Newsletter)
- Sprache & Region
- Gefahrenzone: Account loeschen mit Lizenz-Warnung und "LOESCHEN"-Bestaetigungseingabe

### Benachrichtigungen (`/notifications`)
- Typ-Icons: Info (blau), Warnung (gelb), Erfolg (gruen)
- Ungelesen-Markierung (blauer Punkt und linker Rand)
- Badge-Counter im Header
- Aktionen: Als gelesen markieren (einzeln/alle), Loeschen (einzeln/alle)
- Lizenz-Ablauf-Format: `(****-****-E5F6)` mit letzten 4 Zeichen
- Empty State bei keine Benachrichtigungen

### 404-Seite
- Benutzerfreundliche Fehlerseite mit Zurueck-Link

## Konsistenz-Regeln (nach Bereinigung v2.1)

### Terminologie
- **Lizenz** (nicht Abo, Abonnement oder Tarif)
- **Geraet** (nicht PC, System oder Client)
- **Konto** (nicht Account)
- **Benachrichtigung** (nicht Hinweis, Meldung oder Aktivitaet)
- **Ticket** (nicht Anfrage)

### Status-System
- **Lizenz-Status**: Aktiv (gruen), Laeuft bald ab (gelb), Abgelaufen (rot)
- **Geraete-Status**: Online (gruen), Offline (rot), Wartung (gelb)
- **Sicherheits-Status**: Geschuetzt (gruen), Warnung (gelb), Kritisch (rot)
- **Ticket-Status**: Offen (gelb), In Bearbeitung (blau), Warten auf Antwort (orange), Geschlossen (gruen)

### Datumsformat
- ISO-Timestamps in allen Daten
- Anzeige: `TT.MM.JJJJ, HH:MM` (deutsches Format)
- Relative Zeiten nur fuer Quick-Info (z.B. "vor 3 Min.")

### Datenlogik
- Lizenz-Geraete-Zaehler stimmen mit tatsaechlichen Geraete-Zuweisungen ueberein
- Dashboard-Geraete-Zaehlung basiert auf `devices.length`, nicht auf Lizenz-Slots
- Offene Tickets beinhalten Status "Offen", "In Bearbeitung" und "Warten auf Antwort"
- Profil-Stats werden dynamisch aus Mock-Daten berechnet

## Technologie-Stack

- **React 18** mit TypeScript
- **React Router 7** (Data Mode mit RouterProvider)
- **Tailwind CSS v4** fuer Styling
- **Radix UI** fuer UI-Primitives (Dialog, Select, Switch, etc.)
- **Motion** fuer Animationen
- **Sonner** fuer Toast-Benachrichtigungen
- **TanStack React Query** fuer Server-State-Management
- **Keycloak JS** fuer Authentifizierung
- **Lucide React** fuer Icons

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

## Design-Entscheidungen

- **Kein Firma-Feld** im Profil (bewusst entfernt)
- **Keine IP-Adressen** auf der Geraete-Seite (Datenschutz)
- **Keine Prioritaeten** im Ticket-System (vereinfacht)
- **Kein Newsletter** in den Profil-Benachrichtigungseinstellungen
- **Lizenzschluessel maskiert**: Erste 8 Zeichen als XXXX-XXXX, letzte 4 sichtbar
- **Benachrichtigungen**: Lizenz-Ablauf zeigt letzte 4 Zeichen als `(****-****-E5F6)`
- **React.forwardRef** fuer Button und DialogOverlay (React 18 / Radix UI Kompatibilitaet)
- **Responsive Design**: `p-4 md:p-6`, responsive Ueberschriften, `grid-cols-2 lg:grid-cols-4` Stats-Grids

## Sicherheit

- **Public Client**: Kein Client Secret noetig (sicher fuer Frontend)
- **PKCE**: Proof Key for Code Exchange aktiviert
- **Token Refresh**: Automatische Token-Erneuerung
- **SSO**: Single Sign-On ueber Keycloak
- **Silent Check SSO**: Pruefung der Session ohne User-Interaktion

## URLs

- **Kundenportal**: https://home.pcwaechter.de (https://home.xn--pcwchter-2za.de)
- **Keycloak Login**: https://login.pcwaechter.de (https://login.xn--pcwchter-2za.de)

## Lizenz

(c) 2026 PC-Waechter. Alle Rechte vorbehalten.
