# Release / Updates & Regelbasierte Intelligenz

## Feature-Flag-System

Feature-Flags steuern, welche Funktionen ein Client sieht und verwenden darf.

### Logik

```
Final Features = plan_flags AND override.enabled AND rollout_pct AND version_min AND platform
```

1. **`plans.feature_flags`** (JSONB) – Plan-Level: Darf der Plan das Feature grundsätzlich?
2. **`feature_overrides`** – Ops-Level: Kill-Switch / stufenweiser Rollout

### Standard Feature-Flags per Plan

| Feature | trial | standard | professional | unlimited |
|---|---|---|---|---|
| `auto_fix` | ✗ | ✓ | ✓ | ✓ |
| `reports` | ✗ | ✗ | ✓ | ✓ |
| `priority_support` | ✗ | ✗ | ✓ | ✓ |

### feature_overrides Felder

| Feld | Beschreibung |
|---|---|
| `feature_key` | Feature-Identifier (z.B. `auto_fix`) |
| `enabled` | Kill-Switch: `false` deaktiviert global |
| `rollout_pct` | 0-100 – Prozentsatz der Nutzer |
| `version_min` | Mindest-Agent-Version |
| `platform` | `windows` oder `all` |
| `notes` | Admin-Kommentar |

### Endpoint

`GET /status` → gibt `feature_flags: { feature_key: bool }` zurück – finaler berechneter Status.

---

## Regelbasierte Intelligenz (Rules Engine)

**Keine LLM-Anbindung** – alle Analysen sind deterministisch und regelbasiert.

### Inputs

| Quelle | Daten |
|---|---|
| Telemetrie | CPU-Auslastung, RAM-Auslastung, Disk-Auslastung |
| Konfiguration | Sicherheitseinstellungen, Firewall-Status |
| Security-State | Antivirus-Status, Windows Defender, letzte Scans |
| Trenddaten | 7-Tage / 30-Tage / 90-Tage Verlauf |
| Crash Reports | Absturzhäufigkeit, Fehlertypen |

### Outputs

| Output | Beschreibung |
|---|---|
| Findings | Erkannte Probleme mit Severity (critical/high/medium/low/info) |
| Reasons | Messwerte die zur Erkennung geführt haben |
| Recommendations | Konkrete Handlungsempfehlungen |
| Score | Gesundheitsscore 0-100 |
| Auto-Fix Actions | Optionale automatische Korrekturen (plan-dependent) |

### Severity-Stufen

| Stufe | Bedeutung |
|---|---|
| `critical` | Sofortiger Handlungsbedarf (z.B. kein Virenschutz) |
| `high` | Dringendes Problem (z.B. Disk >90%) |
| `medium` | Relevantes Problem (z.B. RAM >85%) |
| `low` | Warnung (z.B. veraltete Software) |
| `info` | Hinweis (z.B. Empfehlung) |

---

## Update-System (Windows-Agent)

### Update-Workflow

1. Agent prüft regelmäßig Update-Metadaten-Endpoint
2. Vergleicht aktuelle Version mit `latest_version` + `min_supported_version`
3. Falls Update verfügbar: Download Installer
4. Installations-Workflow (kann Neustart erfordern)

### Update-Kanäle

| Kanal | Beschreibung |
|---|---|
| `stable` | Produktions-Releases |
| `beta` | Beta-Kanal für Early Adopters |
| `internal` | Interne Test-Releases |

### Felder Update-Manifest

| Feld | Beschreibung |
|---|---|
| `latest_version` | Aktuelle Zielversion |
| `min_supported_version` | Mindestversion (darunter: Zwangs-Update) |
| `mandatory` | Zwangs-Update ohne Nutzer-Interaktion |
| `download_url` | Installer-URL |
| `release_notes` | Änderungsprotokoll |
| `channel` | `stable` / `beta` / `internal` |

### Telemetrie-Rückmeldung

Agent meldet Update-Ergebnis via `POST /telemetry/update`:
- `status`: `success` / `failed` / `pending`
- `details`: Fehlertext, Version, Zeitstempel
