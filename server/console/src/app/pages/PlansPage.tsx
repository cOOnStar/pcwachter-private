import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { getPlans, upsertPlan, type Plan } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { toast } from "@/components/ui/use-toast";

const DEFAULT_FLAGS = { auto_fix: false, reports: false, priority_support: false };

function centsToEur(cents: number | null): string {
  if (cents == null) return "–";
  return `${(cents / 100).toFixed(2)} €`;
}

function eurToCents(eur: string): number | null {
  const n = parseFloat(eur);
  return isNaN(n) ? null : Math.round(n * 100);
}

function truncate(s: string | null | undefined, max = 24): string {
  if (!s) return "–";
  return s.length > max ? s.slice(0, max) + "…" : s;
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider">{label}</p>
      <p className="text-sm text-[var(--text-primary)] mt-0.5">{value}</p>
    </div>
  );
}

export default function PlansPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<Plan | null>(null);
  const [editEurStr, setEditEurStr] = useState("");
  const [saving, setSaving] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ["plans"],
    queryFn: getPlans,
  });

  const err = error as (Error & { status?: number }) | null;

  function startEdit(p: Plan) {
    const eurVal = p.amount_cents != null
      ? (p.amount_cents / 100).toFixed(2)
      : p.price_eur != null ? String(p.price_eur) : "";
    setEditEurStr(eurVal);
    setEditing({ ...p, feature_flags: p.feature_flags ?? { ...DEFAULT_FLAGS } });
  }

  async function handleSave() {
    if (!editing) return;
    setSaving(true);
    const amountCents = eurToCents(editEurStr);
    try {
      const updated = await upsertPlan(editing.id, {
        label: editing.label,
        price_eur: editing.price_eur,
        duration_days: editing.duration_days,
        max_devices: editing.max_devices,
        is_active: editing.is_active,
        sort_order: editing.sort_order,
        feature_flags: editing.feature_flags,
        grace_period_days: editing.grace_period_days,
        stripe_price_id: editing.stripe_price_id,
        amount_cents: amountCents,
        currency: editing.currency || "eur",
        price_version: editing.price_version,
        stripe_product_id: editing.stripe_product_id,
      });
      toast({ title: `Plan "${updated.label}" gespeichert`, variant: "success" });
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: ["plans"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    } finally {
      setSaving(false);
    }
  }

  function setFlag(key: string, value: boolean) {
    if (!editing) return;
    setEditing({ ...editing, feature_flags: { ...(editing.feature_flags ?? {}), [key]: value } });
  }

  const q = search.trim().toLowerCase();
  const plans = (data?.items ?? []).filter(
    (p) => !q || p.label.toLowerCase().includes(q) || p.id.toLowerCase().includes(q)
  );

  return (
    <div>
      <PageHeader title="Pläne" subtitle="Preise, Laufzeiten und Features konfigurieren" />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <div className="mb-4">
        <Input
          placeholder="Plan suchen…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="max-w-xs"
        />
      </div>

      <div className="flex flex-col gap-3">
        {isLoading ? (
          Array.from({ length: 3 }).map((_, i) => (
            <Card key={i}><Skeleton className="h-24 w-full" /></Card>
          ))
        ) : (
          plans.map((p) => (
            <Card key={p.id}>
              <CardHeader>
                <div className="flex items-center gap-2">
                  <CardTitle>{p.label}</CardTitle>
                  <span className="font-mono text-[0.68rem] text-[var(--text-muted)] bg-[var(--bg-hover)] px-1.5 py-0.5 rounded">{p.id}</span>
                  {!p.is_active && <Badge variant="warning">Inaktiv</Badge>}
                </div>
                <div className="flex gap-2">
                  <Button variant="ghost" size="sm" onClick={() => startEdit(p)}>Bearbeiten</Button>
                  <Button variant="ghost" size="sm" onClick={() => navigate(`/plans/${p.id}`)}>Stripe →</Button>
                </div>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-2 gap-4 lg:grid-cols-6 mb-3">
                  <Stat label="Preis (DB)" value={p.amount_cents != null ? centsToEur(p.amount_cents) : p.price_eur != null ? `${p.price_eur} €` : "–"} />
                  <Stat label="Laufzeit" value={p.duration_days != null ? `${p.duration_days} Tage` : "Lifetime"} />
                  <Stat label="Max Geräte" value={p.max_devices != null ? String(p.max_devices) : "∞"} />
                  <Stat label="Grace Period" value={`${p.grace_period_days} Tage`} />
                  <Stat label="Version" value={`v${p.price_version}`} />
                  <Stat label="Stripe Price ID" value={truncate(p.stripe_price_id)} />
                </div>
                {p.feature_flags && (
                  <div className="flex flex-wrap gap-2">
                    {Object.entries(p.feature_flags).map(([k, v]) => (
                      <Badge key={k} variant={v ? "success" : "neutral"}>
                        {k.replace(/_/g, " ")}: {v ? "✓" : "✗"}
                      </Badge>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          ))
        )}
        {!isLoading && plans.length === 0 && !err && (
          <p className="text-sm text-[var(--text-muted)] py-6 text-center">Keine Pläne gefunden.</p>
        )}
      </div>

      <Dialog open={!!editing} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Plan bearbeiten</DialogTitle>
            <DialogDescription>{editing?.id}</DialogDescription>
          </DialogHeader>
          {editing && (
            <div className="flex flex-col gap-3">
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs text-[var(--text-secondary)] font-medium">Label</label>
                  <Input value={editing.label} onChange={(e) => setEditing({ ...editing, label: e.target.value })} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs text-[var(--text-secondary)] font-medium">Preis (€, wird in cents gespeichert)</label>
                  <Input
                    type="number"
                    step="0.01"
                    placeholder="z.B. 4.99"
                    value={editEurStr}
                    onChange={(e) => setEditEurStr(e.target.value)}
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs text-[var(--text-secondary)] font-medium">Währung</label>
                  <select
                    value={editing.currency || "eur"}
                    onChange={(e) => setEditing({ ...editing, currency: e.target.value })}
                    className="h-9 rounded-md border border-[var(--border)] bg-[var(--surface)] text-sm px-2 text-[var(--text-primary)]"
                  >
                    <option value="eur">EUR</option>
                    <option value="usd">USD</option>
                  </select>
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs text-[var(--text-secondary)] font-medium">Laufzeit (Tage)</label>
                  <Input type="number" value={editing.duration_days ?? ""} onChange={(e) => setEditing({ ...editing, duration_days: e.target.value ? Number(e.target.value) : null })} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs text-[var(--text-secondary)] font-medium">Max Geräte</label>
                  <Input type="number" value={editing.max_devices ?? ""} onChange={(e) => setEditing({ ...editing, max_devices: e.target.value ? Number(e.target.value) : null })} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs text-[var(--text-secondary)] font-medium">Grace Period (Tage)</label>
                  <Input type="number" value={editing.grace_period_days} onChange={(e) => setEditing({ ...editing, grace_period_days: Number(e.target.value) })} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs text-[var(--text-secondary)] font-medium">Reihenfolge</label>
                  <Input type="number" value={editing.sort_order} onChange={(e) => setEditing({ ...editing, sort_order: Number(e.target.value) })} />
                </div>
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs text-[var(--text-secondary)] font-medium">Stripe Price ID (manuell überschreiben)</label>
                <Input value={editing.stripe_price_id ?? ""} placeholder="price_..." onChange={(e) => setEditing({ ...editing, stripe_price_id: e.target.value || null })} />
              </div>
              <label className="flex items-center gap-2 text-sm text-[var(--text-secondary)] cursor-pointer">
                <input type="checkbox" checked={editing.is_active} onChange={(e) => setEditing({ ...editing, is_active: e.target.checked })} className="w-4 h-4 accent-[var(--accent)]" />
                Plan ist aktiv
              </label>
              <div>
                <p className="text-xs text-[var(--text-secondary)] font-medium mb-2">Feature Flags</p>
                <div className="flex flex-col gap-2">
                  {Object.entries(editing.feature_flags ?? DEFAULT_FLAGS).map(([k, v]) => (
                    <label key={k} className="flex items-center gap-2 text-sm text-[var(--text-secondary)] cursor-pointer">
                      <input type="checkbox" checked={Boolean(v)} onChange={(e) => setFlag(k, e.target.checked)} className="w-4 h-4 accent-[var(--accent)]" />
                      {k.replace(/_/g, " ")}
                    </label>
                  ))}
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="ghost" onClick={() => setEditing(null)}>Abbrechen</Button>
            <Button onClick={handleSave} disabled={saving}>
              {saving ? <span className="spinner" /> : "Speichern"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
