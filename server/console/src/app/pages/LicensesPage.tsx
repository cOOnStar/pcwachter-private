import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import {
  getLicenses, generateLicenses, revokeLicense, blockLicense, unblockLicense,
  type License,
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
import CopyButton from "../components/shared/CopyButton";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { useAuth } from "../context/auth-context";
import { formatDate, formatDateTime } from "@/lib/utils";
import { toast } from "@/components/ui/use-toast";

const TIERS = ["trial", "standard", "professional", "unlimited", "custom"];
const PAGE_SIZE = 50;

function stateBadge(state: string) {
  const map: Record<string, "success" | "warning" | "danger" | "info" | "neutral"> = {
    activated: "success", issued: "neutral", revoked: "danger", blocked: "warning",
  };
  return <Badge variant={map[state] ?? "neutral"}>{state}</Badge>;
}

function tierBadge(tier: string) {
  const map: Record<string, "accent" | "info" | "success" | "neutral"> = {
    unlimited: "accent", professional: "info", standard: "success",
  };
  return <Badge variant={map[tier] ?? "neutral"}>{tier}</Badge>;
}

export default function LicensesPage() {
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [stateFilter, setStateFilter] = useState("all");
  const [offset, setOffset] = useState(0);
  const [showGenerate, setShowGenerate] = useState(false);
  const [genTier, setGenTier] = useState("standard");
  const [genQty, setGenQty] = useState(1);
  const [genDuration, setGenDuration] = useState("");
  const [genNotes, setGenNotes] = useState("");
  const [generating, setGenerating] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ["licenses", search, stateFilter, offset],
    queryFn: () => getLicenses({ search: search || undefined, state: stateFilter === "all" ? undefined : stateFilter, limit: PAGE_SIZE, offset }),
  });

  const err = error as (Error & { status?: number }) | null;

  async function handleAction(action: () => Promise<unknown>, successMsg: string) {
    try {
      await action();
      toast({ title: successMsg, variant: "success" });
      queryClient.invalidateQueries({ queryKey: ["licenses"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    }
  }

  async function handleGenerate() {
    setGenerating(true);
    try {
      const res = await generateLicenses(genTier, genQty, genDuration ? Number(genDuration) : null, genNotes || undefined);
      toast({ title: `${res.licenses.length} Lizenz(en) erstellt`, variant: "success" });
      setShowGenerate(false);
      queryClient.invalidateQueries({ queryKey: ["licenses"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    } finally {
      setGenerating(false);
    }
  }

  const columns: Column<License>[] = [
    {
      key: "key",
      header: "Key",
      render: (l) => (
        <div className="flex items-center gap-1">
          <span className="font-mono text-[0.72rem] text-[var(--text-secondary)] truncate max-w-[160px]">
            {l.licenseKey ?? l.id}
          </span>
          <CopyButton value={l.licenseKey ?? l.id} />
        </div>
      ),
    },
    { key: "tier", header: "Tier", render: (l) => tierBadge(l.tier) },
    { key: "state", header: "Status", render: (l) => stateBadge(l.state) },
    { key: "issuedAt", header: "Ausgestellt", render: (l) => <span className="text-xs text-[var(--text-muted)]">{formatDate(l.issuedAt)}</span> },
    { key: "activatedAt", header: "Aktiviert", render: (l) => <span className="text-xs text-[var(--text-muted)]">{formatDateTime(l.activatedAt)}</span> },
    { key: "expiresAt", header: "Läuft ab", render: (l) => <span className="text-xs text-[var(--text-muted)]">{formatDate(l.expiresAt) ?? "∞"}</span> },
    {
      key: "actions",
      header: "",
      render: (l) => isAdmin() ? (
        <div className="flex items-center gap-1">
          {l.state !== "revoked" && l.state !== "blocked" && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => confirm(`Lizenz ${l.licenseKey ?? l.id} widerrufen?`) && handleAction(() => revokeLicense(l.licenseKey ?? l.id), "Lizenz widerrufen")}
            >
              Widerrufen
            </Button>
          )}
          {l.state !== "blocked" && l.state !== "revoked" && (
            <Button
              variant="danger"
              size="sm"
              onClick={() => confirm(`Lizenz sperren?`) && handleAction(() => blockLicense(l.licenseKey ?? l.id), "Lizenz gesperrt")}
            >
              Sperren
            </Button>
          )}
          {l.state === "blocked" && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => handleAction(() => unblockLicense(l.licenseKey ?? l.id), "Lizenz entsperrt")}
            >
              Entsperren
            </Button>
          )}
        </div>
      ) : null,
      className: "w-48",
    },
  ];

  return (
    <div>
      <PageHeader
        title="Lizenzen"
        subtitle="Alle ausgestellten Lizenzkeys"
        action={
          isAdmin() ? (
            <Button size="sm" onClick={() => setShowGenerate(true)}>
              <Plus className="w-4 h-4" />
              Generieren
            </Button>
          ) : undefined
        }
      />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <Card>
        <CardHeader>
          <CardTitle>Lizenzen ({data?.total ?? 0})</CardTitle>
        </CardHeader>
        <CardContent>
          <DataTable
            columns={columns}
            data={data?.items ?? []}
            total={data?.total ?? 0}
            loading={isLoading}
            pageSize={PAGE_SIZE}
            offset={offset}
            onSearch={(q) => { setSearch(q); setOffset(0); }}
            onPageChange={setOffset}
            searchPlaceholder="Key suchen…"
            rowKey={(l) => l.id}
            filters={
              <Select value={stateFilter} onValueChange={(v) => { setStateFilter(v); setOffset(0); }}>
                <SelectTrigger className="w-36">
                  <SelectValue placeholder="Alle Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Alle Status</SelectItem>
                  <SelectItem value="issued">Issued</SelectItem>
                  <SelectItem value="activated">Activated</SelectItem>
                  <SelectItem value="revoked">Revoked</SelectItem>
                  <SelectItem value="blocked">Blocked</SelectItem>
                </SelectContent>
              </Select>
            }
          />
        </CardContent>
      </Card>

      <Dialog open={showGenerate} onOpenChange={setShowGenerate}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Lizenzen generieren</DialogTitle>
            <DialogDescription>Neue Lizenz-Keys für ein Tier erstellen</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-[var(--text-secondary)] font-medium">Tier</label>
              <Select value={genTier} onValueChange={setGenTier}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TIERS.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-[var(--text-secondary)] font-medium">Anzahl (1–100)</label>
              <Input
                type="number"
                min={1}
                max={100}
                value={genQty}
                onChange={(e) => setGenQty(Math.max(1, Math.min(100, Number(e.target.value))))}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-[var(--text-secondary)] font-medium">Laufzeit in Tagen (leer = Lifetime)</label>
              <Input
                type="number"
                placeholder="365"
                value={genDuration}
                onChange={(e) => setGenDuration(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-[var(--text-secondary)] font-medium">Notizen (optional)</label>
              <Input
                placeholder="Interne Notiz…"
                value={genNotes}
                onChange={(e) => setGenNotes(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setShowGenerate(false)}>Abbrechen</Button>
            <Button onClick={handleGenerate} disabled={generating}>
              {generating ? <span className="spinner" /> : "Generieren"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
