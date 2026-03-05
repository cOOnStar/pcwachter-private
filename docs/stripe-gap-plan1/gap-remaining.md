# Rest-Gaps (nach Repo-Review)

✅ Server/API: Stripe Webhook + Checkout + Portal + Price Publishing + Migration (Mode A) sind vorhanden.  
✅ Console UI: Plan-Management + Publish + Stripe-Status ist vorhanden.

⚠️ Haupt-Gap: Home nutzt (teilweise) eigene Stripe-Logik und zeigt teils alte Preisfelder.
- Home sollte nur noch API nutzen (kein Stripe Secret im Home).
- Home sollte `amount_cents/currency/price_version` anzeigen und Kauf nur erlauben, wenn `stripe_price_id` published ist.
- Optional: pro Subscription/User “Self-cancel” Flag + zwei Portal-Konfigurationen.
