# Backend Schema Dokumentation - PC-Waechter Kundenportal

> **Version:** 2.1 (nach Konsistenz-Bereinigung)
> **Letzte Aktualisierung:** 2026-03-06
> **Ziel:** Komplette Backend-Spezifikation fuer API, Datenbank und Keycloak

---

## Inhaltsverzeichnis

1. [Uebersicht](#uebersicht)
2. [Keycloak User Attributes](#keycloak-user-attributes)
3. [Datenbank Schema](#datenbank-schema)
4. [API Endpoints](#api-endpoints)
5. [Datentypen und Validierung](#datentypen-und-validierung)
6. [Beziehungen und Constraints](#beziehungen-und-constraints)
7. [Beispiel-Daten](#beispiel-daten)

---

## Uebersicht

Das PC-Waechter Kundenportal benoetigt:

- **Keycloak** fuer Authentifizierung + User-Management
- **PostgreSQL** fuer Geschaeftsdaten (Lizenzen, Geraete, Tickets, etc.)
- **REST API** fuer Frontend-Backend-Kommunikation
- **Optional: Zammad** fuer Ticket-System

### System-Architektur

```
Frontend (React)                    home.pcwaechter.de
       |
       | HTTPS + JWT Token
       v
Keycloak                            login.pcwaechter.de
  - User Authentication
  - User Attributes
       |
       | User ID
       v
REST API Backend                    api.pcwaechter.de
  - Business Logic
  - Authorization
       |
       v
PostgreSQL Database
  - Licenses, Devices
  - Support Tickets (wenn nicht Zammad)
  - Notifications, Audit Logs
```

---

## Keycloak User Attributes

### Standard Keycloak Felder

| Feld | Typ | Beschreibung | Pflicht |
|------|-----|--------------|---------|
| `id` | UUID | Eindeutige User-ID | Ja |
| `username` | String | Benutzername | Ja |
| `email` | String | E-Mail-Adresse | Ja |
| `emailVerified` | Boolean | E-Mail verifiziert | Nein |
| `firstName` | String | Vorname | Nein |
| `lastName` | String | Nachname | Nein |
| `enabled` | Boolean | Konto aktiv | Ja |
| `createdTimestamp` | Timestamp | Erstellungsdatum | Ja |

### Custom User Attributes

```json
{
  "attributes": {
    "phone": ["+49 123 456789"],
    "license_tier": ["Professional"],
    "customer_id": ["CUST-12345"],
    "language": ["de"]
  }
}
```

| Attribut | Typ | Beschreibung | Beispiel |
|----------|-----|--------------|----------|
| `phone` | String | Telefonnummer | "+49 123 456789" |
| `license_tier` | String | Lizenztyp | "Standard", "Professional" |
| `customer_id` | String | Kunden-Nummer | "CUST-12345" |
| `language` | String | Sprache | "de", "en" |

**Hinweis:** Das Feld `company` (Firma) wird im Frontend nicht verwendet und ist kein Pflichtattribut.

---

## Datenbank Schema

### Technologie-Empfehlung

- **PostgreSQL 17**
- **Encoding:** UTF-8
- **Timezone:** UTC

---

### 1. Tabelle: `users`

**Zweck:** Erweiterte User-Informationen (zusaetzlich zu Keycloak)

```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    keycloak_id UUID NOT NULL UNIQUE,
    customer_id VARCHAR(50) UNIQUE NOT NULL,
    phone VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_users_keycloak_id ON users (keycloak_id);
CREATE INDEX idx_users_customer_id ON users (customer_id);
```

| Feld | Typ | Nullable | Beschreibung |
|------|-----|----------|--------------|
| `id` | INTEGER | NO | Auto-increment ID |
| `keycloak_id` | UUID | NO | Keycloak User ID (eindeutig) |
| `customer_id` | VARCHAR(50) | NO | Kundennummer (z.B. "CUST-12345") |
| `phone` | VARCHAR(50) | YES | Telefonnummer |
| `created_at` | TIMESTAMP | NO | Erstellungsdatum |
| `updated_at` | TIMESTAMP | NO | Letzte Aenderung |

---

### 2. Tabelle: `licenses`

**Zweck:** Lizenzverwaltung

```sql
CREATE TABLE licenses (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    license_key VARCHAR(255) NOT NULL UNIQUE,
    type VARCHAR(50) NOT NULL CHECK (type IN ('Standard', 'Professional')),
    status VARCHAR(50) NOT NULL DEFAULT 'Aktiv' CHECK (status IN ('Aktiv', 'Abgelaufen', 'Laeuft bald ab')),
    valid_until DATE NOT NULL,
    devices_current INTEGER DEFAULT 0,
    devices_max INTEGER NOT NULL CHECK (devices_max >= 1),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT chk_devices_current CHECK (devices_current <= devices_max)
);

CREATE INDEX idx_licenses_user_id ON licenses (user_id);
CREATE INDEX idx_licenses_status ON licenses (status);
CREATE INDEX idx_licenses_valid_until ON licenses (valid_until);
```

| Feld | Typ | Nullable | Beschreibung |
|------|-----|----------|--------------|
| `id` | INTEGER | NO | Auto-increment ID |
| `user_id` | INTEGER | NO | Referenz zu `users.id` |
| `name` | VARCHAR(255) | NO | Lizenzname (z.B. "PC-Waechter Professional") |
| `license_key` | VARCHAR(255) | NO | Lizenzschluessel (maskiert: "XXXX-XXXX-A1B2") |
| `type` | VARCHAR(50) | NO | "Standard" oder "Professional" |
| `status` | VARCHAR(50) | NO | "Aktiv", "Abgelaufen" oder "Laeuft bald ab" |
| `valid_until` | DATE | NO | Gueltig bis Datum |
| `devices_current` | INTEGER | NO | Aktuell zugewiesene Geraete |
| `devices_max` | INTEGER | NO | Maximale Geraete |
| `created_at` | TIMESTAMP | NO | Erstellungsdatum |
| `updated_at` | TIMESTAMP | NO | Letzte Aenderung |

---

### 3. Tabelle: `devices`

**Zweck:** Registrierte Geraete mit PC-Waechter

```sql
CREATE TABLE devices (
    id VARCHAR(50) PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    license_id INTEGER REFERENCES licenses(id) ON DELETE SET NULL,
    name VARCHAR(255) NOT NULL,
    type VARCHAR(50) NOT NULL CHECK (type IN ('Desktop', 'Laptop', 'Server')),
    os VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Offline' CHECK (status IN ('Online', 'Offline', 'Wartung')),
    security_status VARCHAR(50) NOT NULL DEFAULT 'Geschuetzt' CHECK (security_status IN ('Geschuetzt', 'Warnung', 'Kritisch')),
    status_message TEXT,
    pcwaechter_version VARCHAR(20) NOT NULL,
    last_seen TIMESTAMP,
    last_scan TIMESTAMP,
    last_maintenance TIMESTAMP,
    update_available BOOLEAN DEFAULT FALSE,
    latest_version VARCHAR(20),
    registered_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_devices_user_id ON devices (user_id);
CREATE INDEX idx_devices_license_id ON devices (license_id);
CREATE INDEX idx_devices_status ON devices (status);
```

| Feld | Typ | Nullable | Beschreibung |
|------|-----|----------|--------------|
| `id` | VARCHAR(50) | NO | Geraete-ID (z.B. "dev-1") |
| `user_id` | INTEGER | NO | Referenz zu `users.id` |
| `license_id` | INTEGER | YES | Referenz zu `licenses.id` (NULL = nicht zugewiesen) |
| `name` | VARCHAR(255) | NO | Geraetename (z.B. "Calvin-PC") |
| `type` | VARCHAR(50) | NO | "Desktop", "Laptop" oder "Server" |
| `os` | VARCHAR(255) | NO | Betriebssystem (z.B. "Windows 11 Pro") |
| `status` | VARCHAR(50) | NO | "Online", "Offline" oder "Wartung" |
| `security_status` | VARCHAR(50) | NO | "Geschuetzt", "Warnung" oder "Kritisch" |
| `status_message` | TEXT | YES | Statusmeldung (z.B. "Alles in Ordnung") |
| `pcwaechter_version` | VARCHAR(20) | NO | Installierte PC-Waechter Version |
| `last_seen` | TIMESTAMP | YES | Letzter Check-in |
| `last_scan` | TIMESTAMP | YES | Letzter Sicherheitsscan |
| `last_maintenance` | TIMESTAMP | YES | Letzte Wartung |
| `update_available` | BOOLEAN | NO | Update verfuegbar |
| `latest_version` | VARCHAR(20) | YES | Neueste verfuegbare Version |
| `registered_at` | TIMESTAMP | NO | Erste Registrierung |

**Hinweis:** IP-Adressen werden in der Datenbank gespeichert (`ip_address VARCHAR(45)`), aber im Frontend bewusst nicht angezeigt (Datenschutz).

---

### 4. Tabelle: `device_history`

**Zweck:** Ereignis-Verlauf pro Geraet

```sql
CREATE TABLE device_history (
    id VARCHAR(50) PRIMARY KEY,
    device_id VARCHAR(50) NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    type VARCHAR(50) NOT NULL CHECK (type IN ('check-in', 'scan', 'warning', 'action', 'update')),
    message TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_device_history_device_id ON device_history (device_id);
CREATE INDEX idx_device_history_created_at ON device_history (created_at);
```

---

### 5. Tabelle: `license_audit_log`

**Zweck:** Audit-Trail fuer Lizenzaenderungen

```sql
CREATE TABLE license_audit_log (
    id VARCHAR(50) PRIMARY KEY,
    license_id INTEGER NOT NULL REFERENCES licenses(id) ON DELETE CASCADE,
    action VARCHAR(50) NOT NULL CHECK (action IN ('created', 'updated', 'deleted', 'device_added', 'device_removed', 'renewed')),
    description TEXT NOT NULL,
    user_name VARCHAR(255) NOT NULL,
    details JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_license_id ON license_audit_log (license_id);
CREATE INDEX idx_audit_created_at ON license_audit_log (created_at);
```

**Details JSON Struktur:**

```json
{
  "oldValue": { "validUntil": "31.12.2025" },
  "newValue": { "validUntil": "31.12.2026" },
  "deviceInfo": "DESKTOP-WIN11-01 (Windows 11 Pro)"
}
```

---

### 6. Tabelle: `support_tickets`

**Zweck:** Support-Ticket-System

> **Alternative:** Wenn Zammad genutzt wird, entfaellt diese Tabelle.

```sql
CREATE TABLE support_tickets (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title VARCHAR(500) NOT NULL,
    description TEXT NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Offen' CHECK (status IN ('Offen', 'In Bearbeitung', 'Warten auf Antwort', 'Geschlossen')),
    category VARCHAR(100) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_tickets_user_id ON support_tickets (user_id);
CREATE INDEX idx_tickets_status ON support_tickets (status);
CREATE INDEX idx_tickets_created_at ON support_tickets (created_at);
```

| Feld | Typ | Nullable | Beschreibung |
|------|-----|----------|--------------|
| `id` | INTEGER | NO | Auto-increment ID |
| `user_id` | INTEGER | NO | Referenz zu `users.id` |
| `title` | VARCHAR(500) | NO | Ticket-Titel |
| `description` | TEXT | NO | Ticket-Beschreibung |
| `status` | VARCHAR(50) | NO | "Offen", "In Bearbeitung", "Warten auf Antwort", "Geschlossen" |
| `category` | VARCHAR(100) | NO | Kategorie (Installation, Lizenzierung, Technischer Support, Konfiguration, Feedback) |
| `created_at` | TIMESTAMP | NO | Erstellungsdatum |
| `updated_at` | TIMESTAMP | NO | Letzte Aenderung |

---

### 7. Tabelle: `ticket_messages`

**Zweck:** Nachrichten/Antworten in Tickets

```sql
CREATE TABLE ticket_messages (
    id SERIAL PRIMARY KEY,
    ticket_id INTEGER NOT NULL REFERENCES support_tickets(id) ON DELETE CASCADE,
    sender_name VARCHAR(255) NOT NULL,
    message TEXT NOT NULL,
    is_support BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_messages_ticket_id ON ticket_messages (ticket_id);
CREATE INDEX idx_messages_created_at ON ticket_messages (created_at);
```

---

### 8. Tabelle: `ticket_attachments`

**Zweck:** Dateianhhaenge fuer Tickets

```sql
CREATE TABLE ticket_attachments (
    id VARCHAR(50) PRIMARY KEY,
    ticket_id INTEGER NOT NULL REFERENCES support_tickets(id) ON DELETE CASCADE,
    message_id INTEGER REFERENCES ticket_messages(id) ON DELETE CASCADE,
    file_name VARCHAR(500) NOT NULL,
    file_size INTEGER NOT NULL,
    file_type VARCHAR(100) NOT NULL,
    file_path VARCHAR(1000) NOT NULL,
    uploaded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_attachments_ticket_id ON ticket_attachments (ticket_id);
```

---

### 9. Tabelle: `ticket_ratings`

**Zweck:** Bewertungen fuer geschlossene Tickets

```sql
CREATE TABLE ticket_ratings (
    id SERIAL PRIMARY KEY,
    ticket_id INTEGER NOT NULL UNIQUE REFERENCES support_tickets(id) ON DELETE CASCADE,
    rating INTEGER NOT NULL CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

---

### 10. Tabelle: `notifications`

**Zweck:** Benachrichtigungen fuer User

```sql
CREATE TABLE notifications (
    id VARCHAR(50) PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title VARCHAR(500) NOT NULL,
    message TEXT NOT NULL,
    type VARCHAR(20) NOT NULL DEFAULT 'info' CHECK (type IN ('info', 'warning', 'success', 'error')),
    read BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_notifications_user_id ON notifications (user_id);
CREATE INDEX idx_notifications_read ON notifications (read);
CREATE INDEX idx_notifications_created_at ON notifications (created_at);
```

---

### 11. Tabelle: `ticket_templates`

**Zweck:** Vorlagen fuer Tickets

```sql
CREATE TABLE ticket_templates (
    id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    category VARCHAR(100) NOT NULL,
    description TEXT,
    fields JSONB NOT NULL,
    active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

**Fields JSON Struktur:**

```json
[
  {
    "label": "Betriebssystem",
    "type": "select",
    "options": ["Windows 10", "Windows 11", "Windows Server 2022"],
    "required": true
  },
  {
    "label": "Fehlermeldung",
    "type": "textarea",
    "placeholder": "Bitte beschreiben Sie die Fehlermeldung...",
    "required": true
  }
]
```

---

### 12. Tabelle: `documentation`

**Zweck:** Dokumentations-Dateien

```sql
CREATE TABLE documentation (
    id SERIAL PRIMARY KEY,
    name VARCHAR(500) NOT NULL,
    version VARCHAR(50) NOT NULL,
    type VARCHAR(20) NOT NULL CHECK (type IN ('manual', 'guide', 'technical')),
    format VARCHAR(20) NOT NULL,
    language VARCHAR(10) NOT NULL DEFAULT 'de',
    file_size VARCHAR(50) NOT NULL,
    file_path VARCHAR(1000) NOT NULL,
    active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

---

## API Endpoints

### Base URL

```
https://api.pcwaechter.de/v1
```

### Authentication

Alle API-Requests benoetigen ein **JWT Token** von Keycloak im Header:

```http
Authorization: Bearer <JWT_TOKEN>
```

---

### 1. User Endpoints

#### `GET /users/me`

Gibt aktuelle User-Informationen zurueck.

**Response:**

```json
{
  "id": 1,
  "keycloak_id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "customer_id": "CUST-12345",
  "email": "max.mustermann@firma.de",
  "firstName": "Max",
  "lastName": "Mustermann",
  "phone": "+49 123 456789",
  "license_tier": "Professional"
}
```

#### `PUT /users/me`

Aktualisiert User-Informationen.

**Request Body:**

```json
{
  "phone": "+49 987 654321"
}
```

#### `DELETE /users/me`

Loescht das Benutzerkonto. Erfordert Bestaetigungstext "LOESCHEN" im Request.

**Request Body:**

```json
{
  "confirmation": "LOESCHEN"
}
```

---

### 2. License Endpoints

#### `GET /licenses`

Gibt alle Lizenzen des Users zurueck.

**Query Parameters:**

- `status` (optional): "Aktiv", "Abgelaufen", "Laeuft bald ab"
- `type` (optional): "Standard", "Professional"
- `search` (optional): Suchbegriff (Name oder Schluessel)

**Response:**

```json
{
  "licenses": [
    {
      "id": 1,
      "name": "PC-Waechter Professional",
      "license_key": "XXXX-XXXX-A1B2",
      "type": "Professional",
      "status": "Aktiv",
      "valid_until": "2026-12-31",
      "devices_current": 2,
      "devices_max": 3
    }
  ],
  "total": 4,
  "stats": {
    "active": 2,
    "expiring": 1,
    "expired": 1,
    "total_devices_used": 4,
    "total_devices_max": 8
  }
}
```

#### `GET /licenses/{id}/audit-log`

Gibt den Audit-Log einer Lizenz zurueck.

#### `POST /licenses/{id}/renew`

Verlaengerungsantrag fuer eine Lizenz.

---

### 3. Device Endpoints

#### `GET /devices`

Gibt alle Geraete des Users zurueck.

**Query Parameters:**

- `status` (optional): "Online", "Offline", "Wartung"
- `search` (optional): Suchbegriff (Name oder OS)

**Response:**

```json
{
  "devices": [
    {
      "id": "dev-1",
      "name": "Calvin-PC",
      "type": "Desktop",
      "os": "Windows 11 Pro",
      "status": "Online",
      "security_status": "Geschuetzt",
      "status_message": "Alles in Ordnung",
      "license_id": 1,
      "license_name": "PC-Waechter Professional",
      "license_status": "Aktiv",
      "pcwaechter_version": "2.6.0",
      "last_seen": "2026-03-06T11:57:00Z",
      "update_available": false
    }
  ],
  "stats": {
    "total": 5,
    "online": 2,
    "offline": 2,
    "maintenance": 1
  }
}
```

#### `PUT /devices/{id}/rename`

Geraet umbenennen.

```json
{ "name": "Neuer-Name" }
```

#### `DELETE /devices/{id}`

Geraet entkoppeln (Lizenz wird freigegeben).

#### `PUT /devices/{id}/license`

Lizenz zuweisen oder neu zuweisen.

```json
{ "license_id": 3 }
```

---

### 4. Support Ticket Endpoints

#### `GET /tickets`

**Query Parameters:**

- `status` (optional): "Offen", "In Bearbeitung", "Warten auf Antwort", "Geschlossen"
- `category` (optional): Kategorie
- `search` (optional): Suchbegriff

**Response:**

```json
{
  "tickets": [...],
  "stats": {
    "open": 1,
    "in_progress": 2,
    "closed": 2,
    "avg_response_time_hours": 2.5
  }
}
```

#### `POST /tickets`

Neues Ticket erstellen.

#### `POST /tickets/{id}/messages`

Nachricht hinzufuegen.

#### `PUT /tickets/{id}/close`

Ticket schliessen.

#### `POST /tickets/{id}/rating`

Geschlossenes Ticket bewerten (1-5 Sterne).

---

### 5. Notification Endpoints

#### `GET /notifications`

**Query Parameters:**

- `read` (optional): "true" oder "false"
- `type` (optional): "info", "warning", "success", "error"

**Response:**

```json
{
  "notifications": [
    {
      "id": "1",
      "title": "Neue Version verfuegbar",
      "message": "PC-Waechter Version 2.6.0 ist jetzt verfuegbar",
      "type": "info",
      "read": false,
      "timestamp": "2026-03-06T10:00:00Z"
    }
  ],
  "unread_count": 2
}
```

#### `PUT /notifications/{id}/read`

Als gelesen markieren.

#### `PUT /notifications/read-all`

Alle als gelesen markieren.

#### `DELETE /notifications/{id}`

Benachrichtigung loeschen.

#### `DELETE /notifications`

Alle Benachrichtigungen loeschen.

---

### 6. Dashboard Endpoint

#### `GET /dashboard/stats`

```json
{
  "active_licenses": 2,
  "expiring_licenses": 1,
  "total_devices": 5,
  "total_device_slots": 8,
  "open_tickets": 3,
  "unread_notifications": 2
}
```

#### `GET /dashboard/activities`

Letzte Aktivitaeten zurueckgeben.

---

### 7. System Status Endpoint

#### `GET /system/status`

```json
{
  "systems": [
    { "name": "API-Server", "status": "operational", "description": "Alle Systeme funktionieren normal" },
    { "name": "Lizenz-Server", "status": "operational", "description": "Alle Systeme funktionieren normal" },
    { "name": "Download-Server", "status": "operational", "description": "Alle Systeme funktionieren normal" }
  ],
  "overall_status": "operational"
}
```

---

## Datentypen und Validierung

### Enum-Werte

```typescript
type LicenseType = 'Standard' | 'Professional';
type LicenseStatus = 'Aktiv' | 'Abgelaufen' | 'Laeuft bald ab';
type DeviceStatus = 'Online' | 'Offline' | 'Wartung';
type SecurityStatus = 'Geschuetzt' | 'Warnung' | 'Kritisch';
type TicketStatus = 'Offen' | 'In Bearbeitung' | 'Warten auf Antwort' | 'Geschlossen';
type NotificationType = 'info' | 'warning' | 'success' | 'error';
type AuditAction = 'created' | 'updated' | 'deleted' | 'device_added' | 'device_removed' | 'renewed';
type SystemStatus = 'operational' | 'degraded' | 'outage';
type DeviceHistoryType = 'check-in' | 'scan' | 'warning' | 'action' | 'update';
```

### Datei-Upload Limits

```json
{
  "max_file_size": 10485760,
  "max_files_per_ticket": 10,
  "allowed_mime_types": [
    "image/jpeg", "image/png", "image/gif",
    "application/pdf", "text/plain",
    "application/zip", "application/x-zip-compressed"
  ]
}
```

---

## Beziehungen und Constraints

### Entity Relationship Diagram

```
users (1) --------< (N) licenses
   |                    |
   |                    +--< (N) license_audit_log
   |
   +----< (N) devices
   |           |
   |           +--< (N) device_history
   |
   +----< (N) support_tickets
   |             |
   |             +--< (N) ticket_messages
   |             +--< (N) ticket_attachments
   |             +--< (1) ticket_ratings
   |
   +----< (N) notifications

ticket_templates (standalone)
documentation (standalone)
```

### Cascade Rules

- **DELETE User** -> DELETE alle Lizenzen, Geraete, Tickets, Benachrichtigungen
- **DELETE License** -> DELETE Audit-Log Eintraege, SET NULL auf `devices.license_id`
- **DELETE Ticket** -> DELETE Messages, Attachments, Rating
- **DELETE Device** -> DELETE Device-History

---

## Beispiel-Daten

### Komplettes User-Beispiel (passend zu Mock-Daten)

```sql
-- 1. User anlegen
INSERT INTO users (keycloak_id, customer_id, phone)
VALUES ('f47ac10b-58cc-4372-a567-0e02b2c3d479', 'CUST-12345', '+49 123 456789');

-- 2. Lizenzen anlegen (4 Stueck, konsistent mit Frontend)
INSERT INTO licenses (user_id, name, license_key, type, status, valid_until, devices_current, devices_max)
VALUES
(1, 'PC-Waechter Professional', 'XXXX-XXXX-A1B2', 'Professional', 'Aktiv', '2026-12-31', 2, 3),
(1, 'PC-Waechter Standard', 'XXXX-XXXX-C3D4', 'Standard', 'Aktiv', '2026-12-31', 2, 3),
(1, 'PC-Waechter Professional', 'XXXX-XXXX-E5F6', 'Professional', 'Laeuft bald ab', '2026-04-15', 0, 1),
(1, 'PC-Waechter Standard', 'XXXX-XXXX-G7H8', 'Standard', 'Abgelaufen', '2026-01-01', 0, 1);

-- 3. Geraete anlegen (5 Stueck, Zuweisungen konsistent)
INSERT INTO devices (id, user_id, license_id, name, type, os, status, security_status, pcwaechter_version)
VALUES
('dev-1', 1, 1, 'Calvin-PC', 'Desktop', 'Windows 11 Pro', 'Online', 'Geschuetzt', '2.6.0'),
('dev-2', 1, 2, 'Laptop-Wohnzimmer', 'Laptop', 'Windows 10 Pro', 'Offline', 'Warnung', '2.5.1'),
('dev-3', 1, 1, 'SERVER-DC01', 'Server', 'Windows Server 2022', 'Online', 'Geschuetzt', '2.6.0'),
('dev-4', 1, NULL, 'Buero-Laptop', 'Laptop', 'Windows 11 Home', 'Offline', 'Kritisch', '2.4.0'),
('dev-5', 1, 2, 'Arbeits-Desktop', 'Desktop', 'Windows 11 Pro', 'Wartung', 'Warnung', '2.6.0');

-- 4. Tickets erstellen (5 Stueck)
INSERT INTO support_tickets (user_id, title, description, status, category)
VALUES
(1, 'Installation schlaegt fehl', 'Bei der Installation auf Windows 11 tritt ein Fehler auf.', 'Geschlossen', 'Installation'),
(1, 'Frage zur Lizenzaktivierung', 'Wie kann ich meine Lizenz auf einem neuen Geraet aktivieren?', 'Offen', 'Lizenzierung'),
(1, 'Performance-Probleme nach Update', 'Nach dem letzten Update ist das System deutlich langsamer.', 'In Bearbeitung', 'Technischer Support'),
(1, 'Backup-Funktion nicht verfuegbar', 'Die automatische Backup-Funktion erscheint nicht im Menue.', 'Warten auf Antwort', 'Technischer Support'),
(1, 'Netzwerkfreigabe konfigurieren', 'Benoetige Hilfe bei der Einrichtung von Netzwerkfreigaben.', 'Geschlossen', 'Konfiguration');

-- 5. Benachrichtigungen (3 Stueck)
INSERT INTO notifications (id, user_id, title, message, type, read)
VALUES
('1', 1, 'Neue Version verfuegbar', 'PC-Waechter Version 2.6.0 ist jetzt verfuegbar', 'info', false),
('2', 1, 'Lizenz laeuft bald ab', 'Ihre Professional-Lizenz (****-****-E5F6) laeuft in 42 Tagen ab', 'warning', false),
('3', 1, 'Support-Ticket aktualisiert', 'Ticket #3 wurde vom Support-Team beantwortet', 'success', true);
```

---

## Deployment-Checklist

### Backend API

- [ ] PostgreSQL Datenbank erstellen
- [ ] Alle Tabellen anlegen (SQL-Skripte ausfuehren)
- [ ] Indexes anlegen
- [ ] Constraints aktivieren
- [ ] API-Server deployen
- [ ] Environment-Variablen setzen:
  - `DATABASE_URL`
  - `KEYCLOAK_URL`
  - `KEYCLOAK_REALM`
  - `KEYCLOAK_CLIENT_ID`
  - `KEYCLOAK_CLIENT_SECRET`
  - `JWT_SECRET`
  - `FILE_STORAGE_PATH` oder `S3_BUCKET`

### Keycloak

- [ ] Realm erstellen: `pcwaechter-prod`
- [ ] Client erstellen: `home-web`
- [ ] User Attributes konfigurieren: `phone`, `license_tier`, `customer_id`
- [ ] Rollen definieren (optional): `customer`, `admin`, `support`

### File Storage

- [ ] File-Upload-Ordner erstellen (oder S3 Bucket)
- [ ] Permissions setzen
- [ ] Backup-Strategie definieren

---

## Weitere Dokumentation

- **API-Dokumentation**: OpenAPI/Swagger unter `/api/v1/docs`
- **Keycloak Admin Guide**: https://www.keycloak.org/documentation
- **Zammad API**: https://docs.zammad.org/en/latest/api/intro.html


Ende der Backend-Schema-Dokumentation
