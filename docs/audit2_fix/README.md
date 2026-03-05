# PCWächter – audits_fix Pack (v6.3-audit2-fixpack-001)

**Zweck:** Dieses Pack enthält *alle offenen* (oder als `unknown` markierten) Punkte aus **docs/audit2/** als umsetzbare Tasks,
inkl. One‑Shot Reihenfolge, Akzeptanzkriterien und einer Rückmelde‑Vorlage.

**Wichtig:** Dieses Pack ändert zunächst **keinen** produktiven Code automatisch.  
Es ist dafür gedacht, dass **Codex/Claude** die Umsetzung im Repo durchführt, *und du danach* die Ergebnisse über die Vorlage
`docs/audits_fix/REPLY_TEMPLATE.md` zurückmeldest.

## Quick Start (manuell)
1. ZIP ins Repo‑Root entpacken (so dass `docs/audits_fix/` existiert).
2. `docs/audits_fix/02_ONE_SHOT_PLAN.md` lesen (Reihenfolge).
3. Codex mit `docs/audits_fix/CODEX_MASTER_PROMPT.md` laufen lassen.
4. Rückmeldung in `docs/audits_fix/REPLY_TEMPLATE.md` ausfüllen und hier posten.

## Quelle / Basis
- Zielbild: **PCWächter v6.3** (Variante A: GitHub Releases)
- IST: `docs/audit2/*` (Commit: siehe `docs/audit2/README.md`)

## Inhalt
- `01_REMAINING_GAPS.md` – offene Punkte (P0/P1/P2) + Quellen/unknown
- `02_ONE_SHOT_PLAN.md` – One‑Shot Reihenfolge + Abhängigkeiten
- `03_TASKS/*.md` – pro Task: genaue Umsetzungsvorgaben + Tests + Done‑Kriterien
- `04_ACCEPTANCE_AND_SMOKE.md` – DoD + Smoke‑Checks
- `CODEX_MASTER_PROMPT.md` – Prompt zum direkten Ausführen
- `REPLY_TEMPLATE.md` – so meldest du mir das Ergebnis zurück
