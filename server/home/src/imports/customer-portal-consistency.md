Überarbeite diese bestehende PCWächter HOME-/Kundenportal-Datei vollständig auf Konsistenz, aber ohne sichtbares Redesign.

WICHTIG:
- Ändere NICHT die grundsätzliche UX
- Ändere NICHT die Navigationsstruktur
- Ändere NICHT die Seitenreihenfolge
- Ändere NICHT den visuellen Stil
- Ändere NICHT das generelle Layoutprinzip
- Erfinde KEINE neuen Features
- Entferne KEINE bestehenden Kernbereiche
- Füge KEINE neue Designrichtung hinzu

ZIEL:
Diese bestehende Kundenportal-Oberfläche soll vollständig konsistent, logisch schlüssig, sprachlich vereinheitlicht, datenplausibel und technisch realistisch vorbereitet werden, sodass sie wie eine echte produktionsreife, API-gestützte Anwendung wirkt.

Es geht NICHT um ein neues Design.
Es geht ausschließlich darum, ALLE Inkonsistenzen vollständig zu bereinigen.

Bitte prüfe und vereinheitliche systematisch die folgenden Bereiche:

1. Terminologie vollständig vereinheitlichen
Verwende im gesamten Portal immer dieselben Begriffe.
Gleiche alle Begriffe über alle Seiten, Karten, Menüs, Buttons, Tabellen, Hinweise und Dialoge hinweg an.

Besonders vereinheitlichen:
- Lizenz / Abo / Abonnement / Tarif
- Gerät / PC / Client / System
- Konto / Profil / Account
- Benachrichtigung / Meldung / Hinweis / Aktivität
- Support / Ticket / Anfrage
- Verlängerung / Erneuerung / Renewal
- Entfernen / Entkoppeln / Löschen
- Aktiv / Gültig / Laufend
- Abgelaufen / Inaktiv / Beendet

Nutze für dieselbe Funktion oder denselben Zustand niemals unterschiedliche Begriffe an verschiedenen Stellen.

2. Seitenlogik vollständig schärfen
Stelle sicher, dass jede Seite nur die Inhalte zeigt, die wirklich zu ihrer Funktion gehören.

Erwartete Seitenlogik:
- Dashboard / Übersicht = Zusammenfassung und wichtigste Kennzahlen
- Meine Geräte = Gerätebezogene Informationen und Geräteaktionen
- Lizenz & Abo = Tarif, Laufzeit, Slots, Verlängerung, Rechnungs-/Billing-Bezug
- Support = Tickets, Hilfeanfragen, Ticketverlauf, Nachrichten
- Downloads = Client-Download, Plattformen, Versionen, Installationsinfos
- Profil / Konto = persönliche Daten, Kontoeinstellungen, Sicherheits- und Profildaten
- Benachrichtigungen = persönliche Hinweise, Statusmeldungen, Updates, Erinnerungen

Entferne keine Inhalte komplett, aber verschiebe oder formuliere inkonsistente Inhalte so um, dass sie logisch zur jeweiligen Seite passen.

3. Datenlogik vollständig bereinigen
Prüfe alle Mock-/Beispieldaten und sämtliche angezeigten Werte auf vollständige Plausibilität.

Achte besonders auf:
- Geräteanzahl
- belegte Lizenzslots
- freie Slots
- aktive Lizenzen
- Testversionen
- ablaufende Lizenzen
- offene Tickets
- geschlossene Tickets
- Benachrichtigungszähler
- Dashboard-Kennzahlen
- letzte Aktivitäten
- Lizenzzuweisung pro Gerät
- Status pro Gerät
- Supportinhalte passend zur Lizenzsituation

Wenn z. B. 2 von 3 Slots genutzt werden, muss das überall identisch stimmen.
Wenn ein Gerät online ist, muss der letzte Check-in dazu passen.
Wenn eine Professional-Lizenz vorhanden ist, dürfen Supportmeldungen nicht unbegründet so wirken, als gäbe es diese nicht.
Alle Zähler, Karten und Listen sollen inhaltlich widerspruchsfrei zusammenpassen.

4. Statussystem vollständig vereinheitlichen
Nutze überall ein konsistentes Statussystem mit einheitlicher Sprache und einheitlicher visueller Logik.

Beispielhafte Zustände:
- Aktiv
- Läuft bald ab
- Abgelaufen
- Testversion
- Online
- Offline
- Warnung
- Problem erkannt
- Geschützt
- Aktion empfohlen
- Offen
- In Bearbeitung
- Warten auf Antwort
- Gelöst
- Geschlossen

Vereinheitliche:
- Badge-Farben
- Badge-Form
- Statusbezeichnungen
- Reihenfolge der Statusanzeige
- Statusdarstellung in Karten, Tabellen, Listen und Details

Vermeide ähnliche, aber unterschiedliche Bezeichnungen für denselben Zustand.

5. Datums- und Zeitdarstellung final vereinheitlichen
Nutze im gesamten Kundenportal ein einheitliches deutsches Format.

Prüfe besonders:
- Letzter Check-in
- Lizenzablauf
- Verlängerungsdatum
- Rechnungsdatum
- Ticketdatum
- letzte Aktivität
- Benachrichtigungszeit
- Verlaufsdaten
- Profil-Metadaten

Keine gemischten Formate wie:
- vor 2 Stunden
- 02.03.26
- March 2, 2026
- 2026-03-02

Entscheide dich für ein konsistentes deutsches Anzeigeprinzip.
Wenn relative Zeiten genutzt werden, dann nur bewusst und konsistent.

6. Nutzer- und Profildaten vollständig konsistent machen
Die Identität des eingeloggten Nutzers muss überall zusammenpassen.

Prüfe und vereinheitliche:
- Vorname
- Nachname
- Vollname
- Anzeigename
- Username
- E-Mail
- Telefonnummer
- Firmenname, falls vorhanden
- Mitglied seit / Account erstellt / letzte Änderung

Keine gemischten Demo-Identitäten.
Keine hart codierten Beispielpersonen.
Keine widersprüchlichen Nutzernamen zwischen Header, Profil, Support und Aktivitäten.
Alle Profilangaben sollen datengetrieben oder neutral vorbereitet wirken.

7. Benachrichtigungen, Aktivitäten und Hinweise klar trennen
Trenne sauber zwischen:
- Benachrichtigungen
- Aktivitäten
- Warnhinweisen
- Support-Updates
- Systemmeldungen

Wenn mehrere dieser Konzepte vorkommen, müssen sie klar unterscheidbar sein.
Keine doppelte Bedeutung derselben UI-Elemente.
Benachrichtigungszähler, Lesestatus und Badge-Anzeigen sollen einheitlich sein.

8. Komponenten vollständig harmonisieren
Alle wiederkehrenden UI-Komponenten sollen sich wie ein einheitliches Designsystem anfühlen.

Prüfe und angleichen:
- Karten
- Tabellen
- Listen
- Detailblöcke
- Suchfelder
- Filter
- Dropdowns
- Badges
- Status-Chips
- Buttons
- Icon-Buttons
- Links
- Seitentitel
- Bereichsüberschriften
- leere Zustände
- Ladezustände
- Fehlerzustände

Gleiche gleiche Komponenten in Struktur, Benennung, Abstand, Größe und Priorisierung an.

9. Aktionslogik vollständig vereinheitlichen
Alle Aktionen sollen klar benannt, sinnvoll priorisiert und konsistent dargestellt sein.

Besonders prüfen:
- Gerät umbenennen
- Gerät entfernen
- Lizenz upgraden
- Zahlungsdaten verwalten
- Ticket erstellen
- Nachricht senden
- Profil speichern
- Passwort ändern
- Benachrichtigung als gelesen markieren
- Download starten

Vereinheitliche:
- Primäraktionen
- Sekundäraktionen
- Danger-Aktionen
- Link-Aktionen
- Reihenfolge der Buttons
- Beschriftungen
- Icons

Kritische Aktionen wie Entfernen, Entkoppeln, Löschen oder Kündigen sollen klar und konsistent gekennzeichnet sein.

10. Edge States, leere Zustände und Fehlerzustände vervollständigen
Ergänze oder bereinige alle Zustände, die in einer echten Anwendung auftreten können, ohne das UX-Konzept zu ändern.

Beispiele:
- keine Geräte vorhanden
- keine aktive Lizenz
- keine Benachrichtigungen
- keine offenen Tickets
- keine Downloads verfügbar
- Testversion abgelaufen
- Ladezustand
- Fehler beim Laden
- Authentifizierung fehlgeschlagen
- kein Zugriff
- keine Suchergebnisse

Diese Zustände sollen im selben Stil wie das bestehende Portal wirken.
Keine generischen Platzhalter.
Keine unstimmigen Demo-Texte.

11. Mock-/Demo-Anmutung vollständig reduzieren
Ohne die Struktur zu ändern, soll das Portal nicht mehr wie ein loses Mockup wirken.

Bitte:
- entferne offensichtliche Demo-Personen
- entferne unnatürliche Platzhalter
- entferne Beispielwerte, die wie Fake-Daten auffallen
- formuliere Restwerte neutral oder realistisch datengetrieben
- sorge dafür, dass jede angezeigte Information wie ein später echtes Backend-Feld wirkt

Die Oberfläche soll wie eine reale produktive App aussehen, auch wenn noch Beispieldaten verwendet werden.

12. API-/Datenbank-Nähe stärken
Bereite alle Inhalte so vor, dass sie technisch glaubwürdig auf echte Datenfelder abbildbar sind.

Jede Karte, Liste, Kennzahl und Detailansicht soll so wirken, als könne sie direkt aus einer realen API kommen.
Keine willkürlichen Demo-Kombinationen.
Keine unklaren Kennzahlen.
Klare Zuordnung zu realistischen Feldern wie z. B.:
- device_name
- device_status
- device_type
- last_seen_at
- license_status
- subscription_plan
- renewal_date
- seats_used
- seats_total
- notification_read
- ticket_status
- created_at
- updated_at

13. Layout- und Alignment-Feinschliff ohne Redesign
Behalte das bestehende Layout bei, gleiche aber letzte visuelle Inkonsistenzen aus:

- Kartenhöhen
- Innenabstände
- Spaltenausrichtung
- Button-Abstände
- Icon-Größen
- Überschriftenhierarchie
- Tabellenabstände
- Zeilenhöhen
- Kachelgrößen
- Badge-Positionierung
- Konsistenz zwischen Seitenheadern

Das Ergebnis soll ruhiger, professioneller und geschlossener wirken, ohne anders auszusehen.

14. Designsystem implizit konsolidieren
Wende über alle Seiten hinweg konsistente Regeln an für:
- Typografie-Hierarchie
- Spacing
- Radius
- Shadow-Level
- Statusfarben
- Button-Hierarchie
- Badge-Stile
- Input-Felder
- Card-Header
- Tabellenkopf
- Inline-Aktionen

Das bestehende Design soll nicht verändert, sondern systematisch konsolidiert werden.

15. Abschlussziel
Das Ergebnis soll dieselbe PCWächter HOME-/Kundenportal-Oberfläche bleiben, aber:
- vollständig konsistent
- logisch schlüssig
- sprachlich vereinheitlicht
- datenplausibel
- technisch glaubwürdig
- produktionsnäher
- ohne sichtbares Redesign

Wichtig:
Keine neue UX, keine neue Navigation, keine neue Seitenstruktur, keine neuen Features.
Nur vollständig bereinigen, vereinheitlichen und final konsolidieren.