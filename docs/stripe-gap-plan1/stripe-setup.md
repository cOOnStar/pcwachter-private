# PCWächter – Stripe Setup (Mode A: DB = Quelle, Preisänderungen gelten für Bestandskunden)

> Wichtig: Du hast in einem Chat einen **Stripe Secret Key** gepostet. Behandle ihn als kompromittiert und **rotiere ihn sofort** (Stripe Dashboard → Developers → API keys → “Roll key”). Danach `.env`/Secrets aktualisieren.

## 1) Stripe Dashboard (Testmodus)
1. Stripe Dashboard öffnen → **Test mode** aktivieren.
2. **Developers → API keys**
   - `STRIPE_SECRET_KEY` (sk_test_…)
   - `STRIPE_PUBLISHABLE_KEY` (pk_test_…)

## 2) Webhook einrichten (Pflicht)
**Developers → Webhooks → Add endpoint**

- Endpoint URL: `https://<deine-api-domain>/api/v1/payments/webhook`
- Events (mindestens):
  - `checkout.session.completed`
  - `invoice.paid`
  - `invoice.payment_failed`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`

Danach `STRIPE_WEBHOOK_SECRET` (whsec_…) in die API-ENV setzen.

## 3) Customer Portal (optional, aber empfohlen)
Wenn du “Abo verwalten / Rechnungen / Zahlungsmethode ändern” willst:
- **Settings → Billing → Customer portal** aktivieren.
- Features **bewusst** konfigurieren.

### Kündigung standardmäßig AUS, nur bei Bedarf AN
Stripe Portal ist *normalerweise global*. Wenn du “nur für manche Nutzer” willst, nimm **2 Portal-Konfigurationen**:
- `PortalConfig_NoCancel`: Kündigung deaktiviert
- `PortalConfig_CancelAllowed`: Kündigung erlaubt

Dann muss dein Backend beim Erstellen der Portal-Session je nach User/Flag die passende `configuration` setzen.

## 4) PCWächter ENV Variablen
### server/api
- `STRIPE_ENABLED=true`
- `STRIPE_SECRET_KEY=...`
- `STRIPE_PUBLISHABLE_KEY=...`
- `STRIPE_WEBHOOK_SECRET=...`
- `STRIPE_CURRENCY_DEFAULT=eur`

### server/home (wenn Home NICHT direkt Stripe nutzt)
Empfehlung: **Home ohne Stripe Secret** betreiben und nur API-Proxy nutzen.
Dann braucht Home **kein** `STRIPE_SECRET_KEY`.

## 5) Preisverwaltung: “DB ist Quelle”
1. In Console UI den Plan bearbeiten:
   - `amount_cents` setzen (z.B. 499)
   - `currency` (z.B. eur)
2. In Console UI: **Publish Price** (Mode A) ausführen
   - erstellt neues Stripe Price-Objekt
   - migriert aktive Subscriptions auf den neuen Price (proration none)
3. Kontrolle:
   - Stripe Dashboard → Product/Prices: neuer Price aktiv, alter inaktiv
   - nächste Rechnung/Invoice nutzt den neuen Preis
