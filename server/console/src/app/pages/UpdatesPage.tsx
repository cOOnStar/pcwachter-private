import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getUpdateManifests, upsertUpdateManifest, type UpdateManifest } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import CopyButton from "../components/shared/CopyButton";
import { useAuth } from "../context/auth-context";
import { toast } from "@/components/ui/use-toast";
import { Plus, Pencil } from "lucide-react";

const COMPONENTS = ["desktop", "agent", "updater"] as const;
const CHANNELS = ["stable", "beta", "internal"] as const;

type Component = typeof COMPONENTS[number];
type Channel = typeof CHANNELS[number];

function channelBadge(channel: Channel) {
  const map: Record<Channel, "success" | "warning" | "info"> = {
    stable: "success", beta: "warning", internal: "info",
  };
  return <Badge variant={map[channel]}>{channel}</Badge>;
}

function componentBadge(component: Component) {
  const map: Record<Component, "accent" | "info" | "neutral"> = {
    desktop: "accent", agent: "info", updater: "neutral",
  };
  return <Badge variant={map[component]}>{component}</Badge>;
}

const EMPTY_FORM = {
  component: "desktop" as Component,
  channel: "stable" as Channel,
  latest_version: "",
  min_supported_version: "",
  mandatory: false,
  download_url: "",
  sha256: "",
  changelog: "",
  released_at: new Date().toISOString().slice(0, 16),
};

export default function UpdatesPage() {
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [compFilter, setCompFilter] = useState<string>("all");
  const [chanFilter, setChanFilter] = useState<string>("all");
  const [showDialog, setShowDialog] = useState(false);
  const [form, setForm] = useState({ ...EMPTY_FORM });
  const [saving, setSaving] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ["updates", compFilter, chanFilter],
    queryFn: () => getUpdateManifests({
      component: compFilter !== "all" ? compFilter : undefined,
      channel: chanFilter !== "all" ? chanFilter : undefined,
    }),
  });

  const err = error as (Error & { status?: number }) | null;

  function openNew() {
    setForm({ ...EMPTY_FORM });
    setShowDialog(true);
  }

  function openEdit(m: UpdateManifest) {
    setForm({
      component: m.component as Component,
      channel: m.channel as Channel,
      latest_version: m.latest_version,
      min_supported_version: m.min_supported_version,
      mandatory: m.mandatory,
      download_url: m.download_url,
      sha256: m.sha256,
      changelog: m.changelog ?? "",
      released_at: new Date(m.released_at).toISOString().slice(0, 16),
    });
    setShowDialog(true);
  }

  async function handleSave() {
    setSaving(true);
    try {
      await upsertUpdateManifest({
        ...form,
        released_at: new Date(form.released_at).toISOString(),
      });
      toast({ title: "Update-Manifest gespeichert", variant: "success" });
      setShowDialog(false);
      queryClient.invalidateQueries({ queryKey: ["updates"] });
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
        title="Updates"
        subtitle="Update-Manifeste für Desktop, Agent und Updater"
        action={
          isAdmin() ? (
            <Button size="sm" onClick={openNew}>
              <Plus className="w-4 h-4" />
              Manifest anlegen
            </Button>
          ) : undefined
        }
      />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      {/* Filters */}
      <div className="flex gap-2 mb-4 flex-wrap">
        <Select value={compFilter} onValueChange={setCompFilter}>
          <SelectTrigger className="w-36"><SelectValue placeholder="Alle Komponenten" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Alle Komponenten</SelectItem>
            {COMPONENTS.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={chanFilter} onValueChange={setChanFilter}>
          <SelectTrigger className="w-36"><SelectValue placeholder="Alle Channels" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Alle Channels</SelectItem>
            {CHANNELS.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>

      <div className="grid gap-3">
        {isLoading && (
          <Card><CardContent className="py-8 text-center text-[var(--text-muted)] text-sm">Laden…</CardContent></Card>
        )}
        {!isLoading && items.length === 0 && (
          <Card>
            <CardContent className="py-8 text-center text-[var(--text-muted)] text-sm">
              Noch keine Update-Manifeste vorhanden.
            </CardContent>
          </Card>
        )}
        {items.map((m) => (
          <Card key={`${m.component}-${m.channel}`}>
            <CardContent className="pt-4 pb-4">
              <div className="flex items-start justify-between gap-4 flex-wrap">
                <div className="flex items-center gap-2 flex-wrap">
                  {componentBadge(m.component as Component)}
                  {channelBadge(m.channel as Channel)}
                  {m.mandatory && <Badge variant="danger">Mandatory</Badge>}
                </div>
                {isAdmin() && (
                  <Button variant="ghost" size="sm" onClick={() => openEdit(m)}>
                    <Pencil className="w-3.5 h-3.5 mr-1" />
                    Bearbeiten
                  </Button>
                )}
              </div>

              <div className="mt-3 grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
                <div>
                  <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider mb-0.5">Neueste Version</p>
                  <p className="font-mono font-semibold">{m.latest_version}</p>
                </div>
                <div>
                  <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider mb-0.5">Min. unterstützt</p>
                  <p className="font-mono">{m.min_supported_version}</p>
                </div>
                <div className="col-span-2">
                  <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider mb-0.5">Download URL</p>
                  <div className="flex items-center gap-1">
                    <span className="font-mono text-xs text-[var(--text-secondary)] truncate max-w-[480px]">{m.download_url}</span>
                    <CopyButton value={m.download_url} />
                  </div>
                </div>
                <div>
                  <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider mb-0.5">SHA-256</p>
                  <div className="flex items-center gap-1">
                    <span className="font-mono text-[0.7rem] text-[var(--text-secondary)] truncate max-w-[200px]">{m.sha256}</span>
                    <CopyButton value={m.sha256} />
                  </div>
                </div>
                <div>
                  <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider mb-0.5">Veröffentlicht</p>
                  <p className="text-xs text-[var(--text-muted)]">{new Date(m.released_at).toLocaleString("de-DE")}</p>
                </div>
                {m.changelog && (
                  <div className="col-span-2">
                    <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider mb-0.5">Changelog</p>
                    <p className="text-xs text-[var(--text-secondary)] whitespace-pre-line">{m.changelog}</p>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Create/Edit Dialog */}
      <Dialog open={showDialog} onOpenChange={setShowDialog}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Update-Manifest</DialogTitle>
            <DialogDescription>Eindeutig pro Komponente + Channel (Upsert)</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">Komponente</label>
                <Select value={form.component} onValueChange={(v) => setForm((f) => ({ ...f, component: v as Component }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{COMPONENTS.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">Channel</label>
                <Select value={form.channel} onValueChange={(v) => setForm((f) => ({ ...f, channel: v as Channel }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{CHANNELS.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">Neueste Version (semver)</label>
                <Input placeholder="1.4.0" value={form.latest_version} onChange={(e) => setForm((f) => ({ ...f, latest_version: e.target.value }))} />
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-[var(--text-secondary)]">Min. Version</label>
                <Input placeholder="1.2.0" value={form.min_supported_version} onChange={(e) => setForm((f) => ({ ...f, min_supported_version: e.target.value }))} />
              </div>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">Download URL</label>
              <Input placeholder="https://cdn.pcwächter.de/..." value={form.download_url} onChange={(e) => setForm((f) => ({ ...f, download_url: e.target.value }))} />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">SHA-256</label>
              <Input placeholder="abc123..." value={form.sha256} onChange={(e) => setForm((f) => ({ ...f, sha256: e.target.value }))} />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">Veröffentlicht am</label>
              <Input type="datetime-local" value={form.released_at} onChange={(e) => setForm((f) => ({ ...f, released_at: e.target.value }))} />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-[var(--text-secondary)]">Changelog (optional)</label>
              <textarea
                className="w-full rounded-md border border-[var(--border)] bg-[var(--surface)] px-3 py-2 text-sm resize-none text-[var(--text-primary)] placeholder:text-[var(--text-muted)] focus:outline-none focus:ring-1 focus:ring-[var(--accent)]"
                rows={3}
                placeholder="Was ist neu…"
                value={form.changelog}
                onChange={(e) => setForm((f) => ({ ...f, changelog: e.target.value }))}
              />
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="mandatory"
                checked={form.mandatory}
                onChange={(e) => setForm((f) => ({ ...f, mandatory: e.target.checked }))}
                className="w-4 h-4 accent-[var(--accent)]"
              />
              <label htmlFor="mandatory" className="text-sm text-[var(--text-secondary)]">Pflicht-Update (mandatory)</label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setShowDialog(false)}>Abbrechen</Button>
            <Button onClick={handleSave} disabled={saving || !form.latest_version || !form.download_url || !form.sha256}>
              {saving ? <span className="spinner" /> : "Speichern"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
