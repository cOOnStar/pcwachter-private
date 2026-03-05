import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getRules, upsertRule, patchRule, deleteRule,
  getRuleFindings, patchRuleFinding,
  type RuleItem, type FindingItem,
} from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { useAuth } from "../context/auth-context";
import { toast } from "@/components/ui/use-toast";
import { Plus, ToggleLeft, ToggleRight, Trash2 } from "lucide-react";

const CATEGORIES = ["performance", "security", "health", "update", "storage"];
const SEVERITIES = ["critical", "warning", "info"];

function severityBadge(s: string) {
  const map: Record<string, "danger" | "warning" | "info"> = {
    critical: "danger", warning: "warning", info: "info",
  };
  return <Badge variant={map[s] ?? "neutral"}>{s}</Badge>;
}

function stateBadge(s: string) {
  const map: Record<string, "danger" | "neutral" | "success"> = {
    open: "danger", resolved: "success", ignored: "neutral",
  };
  return <Badge variant={map[s] ?? "neutral"}>{s}</Badge>;
}

const EMPTY_RULE: Partial<RuleItem> & { conditions_text: string; recommendations_text: string } = {
  id: "",
  name: "",
  category: "performance",
  severity: "warning",
  enabled: true,
  rollout_percent: 100,
  platform: "all",
  notes: "",
  conditions_text: "[]",
  recommendations_text: '{"text": "", "actions": []}',
};

export default function RulesPage() {
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<"rules" | "findings">("rules");
  const [categoryFilter, setCategoryFilter] = useState("all");
  const [findingState, setFindingState] = useState("open");
  const [showDialog, setShowDialog] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({ ...EMPTY_RULE });
  const [saving, setSaving] = useState(false);

  const { data: rulesData, isLoading: rulesLoading, error: rulesError } = useQuery({
    queryKey: ["rules", categoryFilter],
    queryFn: () => getRules({ category: categoryFilter !== "all" ? categoryFilter : undefined }),
  });

  const { data: findingsData, isLoading: findingsLoading } = useQuery({
    queryKey: ["rule-findings", findingState],
    queryFn: () => getRuleFindings({ state: findingState !== "all" ? findingState : undefined }),
    enabled: tab === "findings",
  });

  const err = rulesError as (Error & { status?: number }) | null;

  function openNew() {
    setEditingId(null);
    setForm({ ...EMPTY_RULE });
    setShowDialog(true);
  }

  function openEdit(r: RuleItem) {
    setEditingId(r.id);
    setForm({
      ...r,
      conditions_text: JSON.stringify(r.conditions, null, 2),
      recommendations_text: JSON.stringify(r.recommendations, null, 2),
    });
    setShowDialog(true);
  }

  async function handleSave() {
    setSaving(true);
    try {
      const conditions = JSON.parse(form.conditions_text || "[]");
      const recommendations = JSON.parse(form.recommendations_text || "{}");
      await upsertRule({
        id: form.id!,
        name: form.name!,
        category: form.category!,
        severity: form.severity ?? "warning",
        enabled: form.enabled ?? true,
        conditions,
        recommendations,
        rollout_percent: form.rollout_percent ?? 100,
        platform: form.platform ?? "all",
        notes: form.notes ?? null,
        min_client_version: null,
        max_client_version: null,
      });
      toast({ title: "Regel gespeichert", variant: "success" });
      setShowDialog(false);
      queryClient.invalidateQueries({ queryKey: ["rules"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    } finally {
      setSaving(false);
    }
  }

  async function handleToggle(r: RuleItem) {
    try {
      await patchRule(r.id, { enabled: !r.enabled });
      toast({ title: r.enabled ? "Regel deaktiviert" : "Regel aktiviert", variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["rules"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    }
  }

  async function handleDelete(r: RuleItem) {
    if (!confirm(`Regel "${r.name}" löschen?`)) return;
    try {
      await deleteRule(r.id);
      toast({ title: "Regel gelöscht", variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["rules"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    }
  }

  async function handleFindingState(f: FindingItem, state: string) {
    try {
      await patchRuleFinding(f.id, { state });
      toast({ title: `Finding ${state}`, variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["rule-findings"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    }
  }

  const ruleColumns: Column<RuleItem>[] = [
    { key: "id", header: "ID", render: (r) => <span className="font-mono text-xs text-[var(--text-muted)]">{r.id}</span> },
    { key: "name", header: "Name", render: (r) => <span className="font-medium">{r.name}</span> },
    { key: "category", header: "Kategorie", render: (r) => <Badge variant="neutral">{r.category}</Badge> },
    { key: "severity", header: "Schwere", render: (r) => severityBadge(r.severity) },
    { key: "enabled", header: "Aktiv", render: (r) => <Badge variant={r.enabled ? "success" : "neutral"}>{r.enabled ? "Ja" : "Nein"}</Badge> },
    { key: "rollout", header: "Rollout", render: (r) => <span className="text-xs">{r.rollout_percent}%</span> },
    {
      key: "actions",
      header: "",
      render: (r) => isAdmin() ? (
        <div className="flex items-center gap-1">
          <Button variant="ghost" size="sm" onClick={() => openEdit(r)}>Bearbeiten</Button>
          <Button variant="ghost" size="sm" onClick={() => handleToggle(r)}>
            {r.enabled ? <ToggleRight className="w-4 h-4 text-green-400" /> : <ToggleLeft className="w-4 h-4" />}
          </Button>
          <Button variant="ghost" size="sm" onClick={() => handleDelete(r)}>
            <Trash2 className="w-4 h-4 text-[var(--red)]" />
          </Button>
        </div>
      ) : null,
      className: "w-44",
    },
  ];

  const findingColumns: Column<FindingItem>[] = [
    { key: "device", header: "Gerät", render: (f) => <span className="text-sm font-medium">{f.device_name ?? f.device_id.slice(0, 8)}</span> },
    { key: "rule", header: "Regel", render: (f) => <span className="text-sm">{f.rule_name ?? f.rule_id}</span> },
    { key: "severity", header: "Schwere", render: (f) => f.severity ? severityBadge(f.severity) : null },
    { key: "state", header: "Status", render: (f) => stateBadge(f.state) },
    { key: "since", header: "Seit", render: (f) => <span className="text-xs text-[var(--text-muted)]">{new Date(f.created_at).toLocaleDateString("de-DE")}</span> },
    {
      key: "actions",
      header: "",
      render: (f) => isAdmin() ? (
        <div className="flex items-center gap-1">
          {f.state !== "resolved" && (
            <Button variant="ghost" size="sm" onClick={() => handleFindingState(f, "resolved")}>Lösen</Button>
          )}
          {f.state !== "ignored" && (
            <Button variant="ghost" size="sm" onClick={() => handleFindingState(f, "ignored")}>Ignorieren</Button>
          )}
          {f.state !== "open" && (
            <Button variant="ghost" size="sm" onClick={() => handleFindingState(f, "open")}>Öffnen</Button>
          )}
        </div>
      ) : null,
      className: "w-44",
    },
  ];

  return (
    <div>
      <PageHeader
        title="Rules Engine"
        subtitle="Regelbasierte Anomalieerkennung und Findings"
        action={
          isAdmin() ? (
            <Button size="sm" onClick={openNew}>
              <Plus className="w-4 h-4" />
              Neue Regel
            </Button>
          ) : undefined
        }
      />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      {/* Tabs */}
      <div className="flex gap-1 mb-4">
        {(["rules", "findings"] as const).map((t) => (
          <Button
            key={t}
            variant={tab === t ? "default" : "ghost"}
            size="sm"
            onClick={() => setTab(t)}
          >
            {t === "rules" ? "Regeln" : "Findings"}
          </Button>
        ))}
      </div>

      {tab === "rules" && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>Regeln ({rulesData?.total ?? 0})</CardTitle>
            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger className="w-40"><SelectValue placeholder="Alle Kategorien" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Alle Kategorien</SelectItem>
                {CATEGORIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
              </SelectContent>
            </Select>
          </CardHeader>
          <CardContent>
            <DataTable
              columns={ruleColumns}
              data={rulesData?.items ?? []}
              total={rulesData?.total ?? 0}
              loading={rulesLoading}
              pageSize={50}
              offset={0}
              rowKey={(r) => r.id}
            />
          </CardContent>
        </Card>
      )}

      {tab === "findings" && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>Findings ({findingsData?.total ?? 0})</CardTitle>
            <Select value={findingState} onValueChange={setFindingState}>
              <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Alle Status</SelectItem>
                <SelectItem value="open">Open</SelectItem>
                <SelectItem value="resolved">Resolved</SelectItem>
                <SelectItem value="ignored">Ignored</SelectItem>
              </SelectContent>
            </Select>
          </CardHeader>
          <CardContent>
            <DataTable
              columns={findingColumns}
              data={findingsData?.items ?? []}
              total={findingsData?.total ?? 0}
              loading={findingsLoading}
              pageSize={100}
              offset={0}
              rowKey={(f) => f.id}
            />
          </CardContent>
        </Card>
      )}

      {/* Rule Dialog */}
      <Dialog open={showDialog} onOpenChange={setShowDialog}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>{editingId ? "Regel bearbeiten" : "Neue Regel"}</DialogTitle>
            <DialogDescription>Conditions + Recommendations als JSON eingeben</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-3 max-h-[60vh] overflow-y-auto pr-1">
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">ID (slug)</label>
                <Input
                  placeholder="cpu_high_sustained"
                  value={form.id ?? ""}
                  onChange={(e) => setForm((f) => ({ ...f, id: e.target.value }))}
                  disabled={!!editingId}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">Name</label>
                <Input
                  placeholder="CPU dauerhaft hoch"
                  value={form.name ?? ""}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                />
              </div>
            </div>
            <div className="grid grid-cols-3 gap-3">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">Kategorie</label>
                <Select value={form.category ?? "performance"} onValueChange={(v) => setForm((f) => ({ ...f, category: v }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{CATEGORIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">Schwere</label>
                <Select value={form.severity ?? "warning"} onValueChange={(v) => setForm((f) => ({ ...f, severity: v }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{SEVERITIES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">Rollout %</label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={form.rollout_percent ?? 100}
                  onChange={(e) => setForm((f) => ({ ...f, rollout_percent: Number(e.target.value) }))}
                />
              </div>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">
                Conditions (JSON Array)
                <span className="ml-1 font-normal text-[var(--text-muted)]">
                  z.B. [{`{"metric":"cpu_percent","operator":">","threshold":90,"category":"cpu"}`}]
                </span>
              </label>
              <textarea
                className="w-full rounded-md border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-xs font-mono resize-none text-[var(--text-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--accent)]"
                rows={4}
                value={form.conditions_text ?? "[]"}
                onChange={(e) => setForm((f) => ({ ...f, conditions_text: e.target.value }))}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">Recommendations (JSON)</label>
              <textarea
                className="w-full rounded-md border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-xs font-mono resize-none text-[var(--text-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--accent)]"
                rows={3}
                value={form.recommendations_text ?? "{}"}
                onChange={(e) => setForm((f) => ({ ...f, recommendations_text: e.target.value }))}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">Notizen (optional)</label>
              <Input
                value={form.notes ?? ""}
                onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
              />
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="rule-enabled"
                checked={form.enabled ?? true}
                onChange={(e) => setForm((f) => ({ ...f, enabled: e.target.checked }))}
                className="w-4 h-4 accent-[var(--accent)]"
              />
              <label htmlFor="rule-enabled" className="text-sm text-[var(--text-secondary)]">Aktiviert</label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setShowDialog(false)}>Abbrechen</Button>
            <Button onClick={handleSave} disabled={saving || !form.id || !form.name}>
              {saving ? <span className="spinner" /> : "Speichern"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
