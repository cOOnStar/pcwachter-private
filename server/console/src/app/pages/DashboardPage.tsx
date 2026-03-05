import { useQuery } from "@tanstack/react-query";
import { Monitor, KeyRound, Activity, Radio } from "lucide-react";
import { getDashboard } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { formatRelative } from "@/lib/utils";

function KpiCard({
  label,
  value,
  sub,
  icon,
  accent = "var(--accent)",
}: {
  label: string;
  value: number | string;
  sub?: string;
  icon: React.ReactNode;
  accent?: string;
}) {
  return (
    <Card
      className="flex flex-col gap-2 relative overflow-hidden"
      style={{ borderLeft: `3px solid ${accent}` }}
    >
      <div
        className="absolute inset-0 pointer-events-none rounded-xl"
        style={{ background: `linear-gradient(135deg, ${accent}12 0%, transparent 60%)` }}
      />
      <div className="flex items-center justify-between relative">
        <span className="text-[0.7rem] text-[var(--text-muted)] uppercase tracking-wider font-semibold">{label}</span>
        <span style={{ color: accent, opacity: 0.7 }}>{icon}</span>
      </div>
      <div className="text-3xl font-bold text-[var(--text-primary)] leading-none relative">{value}</div>
      {sub && <span className="text-xs text-[var(--text-secondary)] relative">{sub}</span>}
    </Card>
  );
}

export default function DashboardPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ["dashboard"],
    queryFn: getDashboard,
  });

  const err = error as (Error & { status?: number }) | null;

  return (
    <div>
      <PageHeader title="Dashboard" subtitle="Übersicht PCWächter-Infrastruktur" />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <div className="grid grid-cols-2 gap-3 mb-6 lg:grid-cols-4">
        {isLoading ? (
          Array.from({ length: 4 }).map((_, i) => (
            <Card key={i}><Skeleton className="h-16 w-full" /></Card>
          ))
        ) : data ? (
          <>
            <KpiCard label="Geräte gesamt" value={data.kpis.totalDevices} sub={`${data.kpis.onlineDevices} online`} icon={<Monitor className="w-4 h-4" />} accent="var(--accent)" />
            <KpiCard label="Online" value={data.kpis.onlineDevices} icon={<Activity className="w-4 h-4" />} accent="var(--success)" />
            <KpiCard label="Telemetrie 24h" value={data.kpis.telemetry24h} icon={<Radio className="w-4 h-4" />} accent="var(--info)" />
            <KpiCard label="Lizenzen aktiv" value={data.kpis.activeLicenses} sub={`von ${data.kpis.totalLicenses} gesamt`} icon={<KeyRound className="w-4 h-4" />} accent="var(--warning)" />
          </>
        ) : null}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Letzte Aktivitäten</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex flex-col gap-3">
              {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : !data?.recentActivity?.length ? (
            <p className="text-sm text-[var(--text-muted)] py-8 text-center">Keine Aktivitäten</p>
          ) : (
            <div className="flex flex-col divide-y divide-[var(--border-muted)]">
              {data.recentActivity.map((a) => (
                <div key={a.id} className="flex items-start justify-between py-3 gap-4">
                  <div className="flex-1 min-w-0">
                    <p className="text-sm text-[var(--text-primary)] font-medium truncate">{a.action}</p>
                    {a.description && <p className="text-xs text-[var(--text-muted)] mt-0.5 truncate">{a.description}</p>}
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    {a.severity && (
                      <Badge variant={a.severity === "error" ? "danger" : a.severity === "warning" ? "warning" : "neutral"}>
                        {a.severity}
                      </Badge>
                    )}
                    <span className="text-xs text-[var(--text-muted)]">{formatRelative(a.timestamp)}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
