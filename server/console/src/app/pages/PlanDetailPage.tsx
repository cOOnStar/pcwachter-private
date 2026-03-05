import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getPlan, getPlanStripeStatus, publishPlanPrice, type PublishPriceResult } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import CopyButton from "../components/shared/CopyButton";

function Field({ label, value, mono = false }: { label: string; value: string | number | null | undefined; mono?: boolean }) {
  const display = value == null || value === "" ? "–" : String(value);
  return (
    <div>
      <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider mb-0.5">{label}</p>
      <p className={`text-sm text-[var(--text-primary)] ${mono ? "font-mono" : ""} flex items-center gap-1.5`}>
        {display}
        {mono && value && <CopyButton value={String(value)} />}
      </p>
    </div>
  );
}

function centsToEur(cents: number | null | undefined): string {
  if (cents == null) return "–";
  return `${(cents / 100).toFixed(2)} €`;
}

export default function PlanDetailPage() {
  const { planId } = useParams<{ planId: string }>();
  const navigate = useNavigate();

  const [publishing, setPublishing] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [result, setResult] = useState<PublishPriceResult | null>(null);
  const [publishError, setPublishError] = useState("");

  const {
    data: plan,
    isLoading: planLoading,
    error: planError,
  } = useQuery({
    queryKey: ["plans", planId],
    queryFn: () => getPlan(planId!),
    enabled: !!planId,
  });

  const {
    data: stripeStatus,
    isLoading: stripeLoading,
    error: stripeError,
    refetch: refetchStripe,
  } = useQuery({
    queryKey: ["plan-stripe-status", planId],
    queryFn: () => getPlanStripeStatus(planId!),
    enabled: !!planId,
    retry: false,
  });

  const planErr = planError as (Error & { status?: number }) | null;
  const stripeErr = stripeError as (Error & { status?: number }) | null;

  async function runPublish(dryRun: boolean) {
    if (!planId) return;
    setPublishing(true);
    setPublishError("");
    setResult(null);
    try {
      const res = await publishPlanPrice(planId, { mode: "A", dry_run: dryRun });
      setResult(res);
      if (!dryRun) {
        refetchStripe();
      }
    } catch (e: unknown) {
      const err = e as Error & { status?: number };
      if (err.status === 401 || err.status === 403) {
        setPublishError("Keine Berechtigung für diese Aktion.");
      } else {
        setPublishError(err.message || "Unbekannter Fehler");
      }
    } finally {
      setPublishing(false);
      setConfirmOpen(false);
    }
  }

  return (
    <div>
      <PageHeader
        title={plan ? `Plan: ${plan.label}` : "Plan Detail"}
        subtitle={planId}
      />
      <div className="mb-4">
        <Button variant="ghost" size="sm" onClick={() => navigate("/plans")}>← Zurück zu Plänen</Button>
      </div>

      {planErr && <div className="mb-4"><ErrorBanner message={planErr.message} status={planErr.status} /></div>}

      {/* Plan Details */}
      <Card className="mb-4">
        <CardHeader>
          <CardTitle>Plan-Details</CardTitle>
          <Button variant="ghost" size="sm" onClick={() => navigate("/plans")}>Bearbeiten (Planliste)</Button>
        </CardHeader>
        <CardContent>
          {planLoading ? (
            <Skeleton className="h-16 w-full" />
          ) : plan ? (
            <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
              <Field label="ID" value={plan.id} mono />
              <Field label="Label" value={plan.label} />
              <Field label="Preis (amount_cents)" value={centsToEur(plan.amount_cents)} />
              <Field label="Währung" value={plan.currency?.toUpperCase()} />
              <Field label="Price Version" value={plan.price_version} />
              <Field label="Laufzeit" value={plan.duration_days != null ? `${plan.duration_days} Tage` : "Lifetime"} />
              <Field label="Max Geräte" value={plan.max_devices ?? "∞"} />
              <Field label="Aktiv" value={plan.is_active ? "Ja" : "Nein"} />
            </div>
          ) : null}
        </CardContent>
      </Card>

      {/* Stripe Status */}
      <Card className="mb-4">
        <CardHeader>
          <CardTitle>Stripe Status</CardTitle>
        </CardHeader>
        <CardContent>
          {stripeLoading ? (
            <Skeleton className="h-24 w-full" />
          ) : stripeErr ? (
            <ErrorBanner message={stripeErr.message} status={stripeErr.status} />
          ) : stripeStatus ? (
            <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
              <Field label="Stripe Product ID" value={stripeStatus.stripe_product_id} mono />
              <Field label="Stripe Price ID (aktuell)" value={stripeStatus.stripe_price_id} mono />
              <Field label="Price Version" value={stripeStatus.price_version} />
              <Field label="Betrag (Stripe)" value={centsToEur(stripeStatus.amount_cents)} />
              <Field label="Währung" value={stripeStatus.currency?.toUpperCase()} />
              <Field label="Aktive Abos" value={stripeStatus.count_active_subs} />
            </div>
          ) : (
            <p className="text-sm text-[var(--text-muted)]">Keine Stripe-Daten verfügbar. Stripe ggf. nicht initialisiert.</p>
          )}
        </CardContent>
      </Card>

      {/* Publish Panel */}
      <Card>
        <CardHeader>
          <CardTitle>Preis publizieren</CardTitle>
        </CardHeader>
        <CardContent>
          <Alert className="mb-4">
            <AlertDescription>
              <strong>Mode A:</strong> Neuer Preis gilt ab nächster Verlängerung. Keine Proration.
              Bestehende Abos werden auf den neuen Stripe-Preis migriert.
            </AlertDescription>
          </Alert>

          <div className="flex gap-3 flex-wrap mb-4">
            <Button
              variant="outline"
              onClick={() => runPublish(true)}
              disabled={publishing}
            >
              {publishing ? <span className="spinner mr-2" /> : null}
              Dry-Run
            </Button>
            <Button
              onClick={() => setConfirmOpen(true)}
              disabled={publishing}
            >
              Preis publizieren…
            </Button>
          </div>

          {publishError && (
            <div className="mb-4">
              <ErrorBanner message={publishError} />
            </div>
          )}

          {result && (
            <div
              className="rounded-lg border border-[var(--border)] p-4 bg-[var(--surface)]"
            >
              <p className="text-sm font-semibold text-[var(--text-primary)] mb-3">
                Ergebnis {result.new_price_id === "(dry-run)" ? "(Dry-Run)" : ""}
              </p>
              <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 mb-3">
                <Field label="Alter Price ID" value={result.old_price_id} mono />
                <Field label="Neuer Price ID" value={result.new_price_id} mono />
                <Field label="Mode" value={result.mode} />
                <Field label="Migriert" value={result.migrated} />
                <Field label="Fehlgeschlagen" value={result.failed} />
                <Field label="Dauer" value={`${result.took_ms} ms`} />
              </div>
              {result.failed_subscription_ids.length > 0 && (
                <div>
                  <p className="text-xs text-[var(--text-muted)] uppercase tracking-wider mb-1">
                    Fehlgeschlagene Subscription IDs ({result.failed_subscription_ids.length})
                  </p>
                  <div className="flex flex-col gap-0.5">
                    {result.failed_subscription_ids.slice(0, 20).map((id) => (
                      <span key={id} className="font-mono text-xs text-[var(--text-secondary)]">{id}</span>
                    ))}
                    {result.failed_subscription_ids.length > 20 && (
                      <span className="text-xs text-[var(--text-muted)]">
                        … und {result.failed_subscription_ids.length - 20} weitere
                      </span>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Confirm Dialog */}
      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>Preis publizieren?</DialogTitle>
            <DialogDescription>
              Erstellt einen neuen Stripe-Preis und migriert alle aktiven Abos (Mode A, keine Proration).
              Diese Aktion kann nicht rückgängig gemacht werden.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setConfirmOpen(false)}>Abbrechen</Button>
            <Button onClick={() => runPublish(false)} disabled={publishing}>
              {publishing ? <span className="spinner mr-2" /> : null}
              Ja, publizieren
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
