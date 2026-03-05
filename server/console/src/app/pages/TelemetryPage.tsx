import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getTelemetry, getTelemetryChart, type TelemetryItem } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { formatDateTime } from "@/lib/utils";
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from "recharts";

const CATEGORIES = ["memory", "ssd", "antivirus", "update"];
const PAGE_SIZE = 50;

function severityBadge(s: string) {
  const map: Record<string, "danger" | "warning" | "info" | "neutral"> = { critical: "danger", error: "danger", warning: "warning", info: "info" };
  return <Badge variant={map[s] ?? "neutral"}>{s}</Badge>;
}

export default function TelemetryPage() {
  const [category, setCategory] = useState("all");
  const [offset, setOffset] = useState(0);
  const [chartCategory, setChartCategory] = useState("memory");
  const [chartHours, setChartHours] = useState(24);

  const { data, isLoading, error } = useQuery({
    queryKey: ["telemetry", category, offset],
    queryFn: () => getTelemetry({ limit: PAGE_SIZE, offset, category: category === "all" ? undefined : category }),
  });

  const { data: chartRaw } = useQuery({
    queryKey: ["telemetry-chart", chartCategory, chartHours],
    queryFn: () => getTelemetryChart(chartCategory, chartHours),
    select: (r) => {
      const grouped: Record<string, number> = {};
      r.points.forEach((p) => {
        const hour = new Date(p.timestamp).toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" });
        grouped[hour] = (grouped[hour] ?? 0) + 1;
      });
      return Object.entries(grouped).map(([time, count]) => ({ time, count }));
    },
  });

  const err = error as (Error & { status?: number }) | null;

  const columns: Column<TelemetryItem>[] = [
    { key: "time", header: "Zeit", render: (t) => <span className="font-mono text-xs">{formatDateTime(t.receivedAt)}</span> },
    { key: "device", header: "Gerät", render: (t) => <span className="font-medium text-[var(--text-primary)]">{t.device || "–"}</span> },
    { key: "category", header: "Kategorie", render: (t) => <Badge variant="neutral">{t.category}</Badge> },
    { key: "summary", header: "Zusammenfassung", render: (t) => <span className="text-xs truncate max-w-[240px] block">{t.summary || "–"}</span> },
    { key: "source", header: "Quelle", render: (t) => <span className="text-xs text-[var(--text-muted)]">{t.source}</span> },
    { key: "severity", header: "Schwere", render: (t) => severityBadge(t.severity) },
  ];

  return (
    <div>
      <PageHeader title="Telemetrie" subtitle="Eingehende Gerätedaten und Sicherheitsstatus" />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <Card className="mb-4">
        <CardHeader>
          <CardTitle>Verlauf</CardTitle>
          <div className="flex items-center gap-2">
            <Select value={chartCategory} onValueChange={setChartCategory}>
              <SelectTrigger className="w-32">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CATEGORIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={String(chartHours)} onValueChange={(v) => setChartHours(Number(v))}>
              <SelectTrigger className="w-28">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="6">6 Std.</SelectItem>
                <SelectItem value="24">24 Std.</SelectItem>
                <SelectItem value="48">48 Std.</SelectItem>
                <SelectItem value="168">7 Tage</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardHeader>
        <CardContent>
          {!chartRaw?.length ? (
            <p className="text-sm text-[var(--text-muted)] py-4 text-center">Keine Chart-Daten</p>
          ) : (
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={chartRaw}>
                <CartesianGrid strokeDasharray="3 3" stroke="#1e2d4f" />
                <XAxis dataKey="time" tick={{ fill: "#5a6a95", fontSize: 11 }} />
                <YAxis tick={{ fill: "#5a6a95", fontSize: 11 }} allowDecimals={false} />
                <Tooltip contentStyle={{ background: "#11182b", border: "1px solid #1e2d4f", borderRadius: 8 }} />
                <Line type="monotone" dataKey="count" stroke="#4f7df5" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Snapshots ({data?.total ?? 0})</CardTitle>
          <Select value={category} onValueChange={(v) => { setCategory(v); setOffset(0); }}>
            <SelectTrigger className="w-40">
              <SelectValue placeholder="Alle Kategorien" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Alle Kategorien</SelectItem>
              {CATEGORIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
            </SelectContent>
          </Select>
        </CardHeader>
        <CardContent>
          <DataTable
            columns={columns}
            data={data?.items ?? []}
            total={data?.total ?? 0}
            loading={isLoading}
            pageSize={PAGE_SIZE}
            offset={offset}
            onPageChange={setOffset}
            rowKey={(t) => t.id}
          />
        </CardContent>
      </Card>
    </div>
  );
}
