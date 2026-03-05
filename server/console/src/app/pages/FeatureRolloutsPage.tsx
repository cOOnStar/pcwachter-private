import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ShieldOff, Plus } from "lucide-react";
import {
  getFeatureOverrides, upsertFeatureOverride, disableFeature, type FeatureOverride,
} from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import EmptyState from "../components/shared/EmptyState";
import { useAuth } from "../context/auth-context";
import { formatDateTime } from "@/lib/utils";
import { toast } from "@/components/ui/use-toast";

type UpsertForm = {
  feature_key: string;
  enabled: boolean;
  rollout_percent: number;
  scope: string;
  target_id: string;
  version_min: string;
  platform: string;
  notes: string;
};

const DEFAULT_FORM: UpsertForm = {
  feature_key: "",
  enabled: true,
  rollout_percent: 100,
  scope: "global",
  target_id: "",
  version_min: "",
  platform: "all",
  notes: "",
};

export default function FeatureRolloutsPage() {
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [showDialog, setShowDialog] = useState(false);
  const [form, setForm] = useState<UpsertForm>(DEFAULT_FORM);
  const [saving, setSaving] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ["feature-overrides"],
    queryFn: getFeatureOverrides,
  });

  const err = error as (Error & { status?: number }) | null;

  async function handleDisable(fo: FeatureOverride) {
    if (!confirm(`Feature "${fo.feature_key}" global deaktivieren (Kill-Switch)?`)) return;
    try {
      await disableFeature(fo.feature_key);
      toast({ title: `Feature "${fo.feature_key}" deaktiviert`, variant: "warning" });
      queryClient.invalidateQueries({ queryKey: ["feature-overrides"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    }
  }

  async function handleSave() {
    setSaving(true);
    try {
      await upsertFeatureOverride({
        feature_key: form.feature_key,
        enabled: form.enabled,
        rollout_percent: form.rollout_percent,
        scope: form.scope,
        target_id: form.scope !== "global" ? (form.target_id || null) : null,
        version_min: form.version_min || null,
        platform: form.platform,
        notes: form.notes || null,
      });
      toast({ title: "Feature Override gespeichert", variant: "success" });
      setShowDialog(false);
      setForm(DEFAULT_FORM);
      queryClient.invalidateQueries({ queryKey: ["feature-overrides"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    } finally {
      setSaving(false);
    }
  }

  const items = data?.items ?? [];

  return (
    <div>
      <PageHeader
        title="Feature Rollouts"
        subtitle="Kill-Switches und stufenweiser Rollout"
        action={
          isAdmin() ? (
            <Button size="sm" onClick={() => { setForm(DEFAULT_FORM); setShowDialog(true); }}>
              <Plus className="w-4 h-4" />
              Override hinzufügen
            </Button>
          ) : undefined
        }
      />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <Card>
        <CardHeader>
          <CardTitle>Feature Overrides ({items.length})</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Feature Key</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Rollout %</TableHead>
                <TableHead>Scope</TableHead>
                <TableHead>Target ID</TableHead>
                <TableHead>Platform</TableHead>
                <TableHead>Aktualisiert</TableHead>
                {isAdmin() && <TableHead></TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 4 }).map((_, i) => (
                  <TableRow key={i}>
                    {Array.from({ length: 8 }).map((__, j) => (
                      <TableCell key={j}><Skeleton className="h-4 w-full" /></TableCell>
                    ))}
                  </TableRow>
                ))
              ) : items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={8} className="p-0">
                    <EmptyState message="Keine Feature Overrides konfiguriert" />
                  </TableCell>
                </TableRow>
              ) : (
                items.map((fo) => (
                  <TableRow key={fo.id}>
                    <TableCell className="font-mono text-sm text-[var(--text-primary)] font-medium">{fo.feature_key}</TableCell>
                    <TableCell>
                      <Badge variant={fo.enabled ? "success" : "danger"}>{fo.enabled ? "Aktiv" : "Deaktiviert"}</Badge>
                    </TableCell>
                    <TableCell>
                      <span className={fo.rollout_percent < 100 ? "text-[var(--warning)]" : ""}>{fo.rollout_percent}%</span>
                    </TableCell>
                    <TableCell><Badge variant="neutral">{fo.scope}</Badge></TableCell>
                    <TableCell className="font-mono text-xs text-[var(--text-muted)] max-w-[120px] truncate">{fo.target_id || "–"}</TableCell>
                    <TableCell className="text-xs text-[var(--text-muted)]">{fo.platform}</TableCell>
                    <TableCell className="text-xs text-[var(--text-muted)]">{formatDateTime(fo.updated_at)}</TableCell>
                    {isAdmin() && (
                      <TableCell>
                        <div className="flex gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => { setForm({ feature_key: fo.feature_key, enabled: fo.enabled, rollout_percent: fo.rollout_percent, scope: fo.scope, target_id: fo.target_id ?? "", version_min: fo.version_min ?? "", platform: fo.platform, notes: fo.notes ?? "" }); setShowDialog(true); }}
                          >
                            Bearbeiten
                          </Button>
                          {fo.enabled && (
                            <Button variant="danger" size="sm" onClick={() => handleDisable(fo)}>
                              <ShieldOff className="w-3.5 h-3.5" />
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    )}
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={showDialog} onOpenChange={setShowDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Feature Override</DialogTitle>
            <DialogDescription>Rollout-Parameter für ein Feature konfigurieren</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-[var(--text-secondary)] font-medium">Feature Key *</label>
              <Input
                placeholder="z.B. new_dashboard"
                value={form.feature_key}
                onChange={(e) => setForm({ ...form, feature_key: e.target.value })}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs text-[var(--text-secondary)] font-medium">Rollout %</label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={form.rollout_percent}
                  onChange={(e) => setForm({ ...form, rollout_percent: Number(e.target.value) })}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs text-[var(--text-secondary)] font-medium">Platform</label>
                <Input
                  placeholder="all / windows / mac"
                  value={form.platform}
                  onChange={(e) => setForm({ ...form, platform: e.target.value })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs text-[var(--text-secondary)] font-medium">Scope</label>
                <Input
                  placeholder="global / plan / user / device"
                  value={form.scope}
                  onChange={(e) => setForm({ ...form, scope: e.target.value })}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs text-[var(--text-secondary)] font-medium">Target ID (wenn nicht global)</label>
                <Input
                  placeholder="plan_id / user_id / device_id"
                  value={form.target_id}
                  disabled={form.scope === "global"}
                  onChange={(e) => setForm({ ...form, target_id: e.target.value })}
                />
              </div>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-[var(--text-secondary)] font-medium">Min. Agent-Version</label>
              <Input
                placeholder="z.B. 2.0.0"
                value={form.version_min}
                onChange={(e) => setForm({ ...form, version_min: e.target.value })}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-[var(--text-secondary)] font-medium">Notizen</label>
              <Input
                placeholder="Interne Notiz…"
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
              />
            </div>
            <label className="flex items-center gap-2 text-sm text-[var(--text-secondary)] cursor-pointer">
              <input
                type="checkbox"
                checked={form.enabled}
                onChange={(e) => setForm({ ...form, enabled: e.target.checked })}
                className="w-4 h-4 accent-[var(--accent)]"
              />
              Feature aktiviert
            </label>
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setShowDialog(false)}>Abbrechen</Button>
            <Button onClick={handleSave} disabled={saving || !form.feature_key}>
              {saving ? <span className="spinner" /> : "Speichern"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
