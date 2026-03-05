# console-gap-plan3 — Vollständiger Rest-Plan (Support + Zammad + P2 Backlog)

Stand: 2026-03-05 (Europe/Berlin)

## Ausgangslage (IST)
- console-gap-plan1 + plan2 sind umgesetzt (UI-Seiten + Backend-Endpunkte für Activity Feed + Knowledge Base + Update-Channel).
- Support UI existiert (Console) und ruft Support-API auf.
- Support-API (Zammad Proxy) existiert, ist aber erst **funktional**, wenn Zammad korrekt erreichbar/konfiguriert ist (ENV + Token + Proxy/DNS).
- Einige Zielbild-Punkte sind bewusst als P2/blocked markiert (UpdatesPage/ClientConfig/Rules UI/KB CRUD).

## Ziel dieses Plan3
1) Support end-to-end produktiv machen (Console + Home + API + Zammad).
2) Zammad korrekt einrichten (Docker/Proxy/ENV + API Token + Groups/Orgs + Webhook optional).
3) UX-Polish: klare Admin-Hinweise statt generischer ErrorBanner + integrierte Diag in der Console.
4) P2 Backlog vollständig dokumentieren (inkl. welche Specs/Quellen fehlen).

## Regeln
- Idempotent: Wiederholbares Setup ohne “kaputt machen”.
- Keine Code-Signing/Authenticode Themen.
- Nichts erfinden: Wenn Specs fehlen → **unknown** + Quelle benennen, was benötigt wird.
