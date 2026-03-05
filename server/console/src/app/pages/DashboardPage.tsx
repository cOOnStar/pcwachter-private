import { useQuery } from "@tanstack/react-query";
import { Monitor, KeyRound, Activity, Radio, ArrowUpRight } from "lucide-react";
import { getDashboard } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { formatRelative } from "@/lib/utils";

interface KpiCardProps {
  label: string;
  value: number | string;
  sub?: string;
  icon: React.ReactNode;
  accent: string;
  trend?: string;
}

function KpiCard({ label, value, sub, icon, accent, trend }: KpiCardProps) {
  return (
    <div
      className="rounded-xl p-5 flex flex-col gap-3 relative overflow-hidden group"
      style={{
        background: "var(--bg-card)",
        border: "1px solid rgba(255,255,255,0.055)",
        boxShadow: "0 4px 20px rgba(0,0,0,0.4)",
      }}
    >
      {/* Glow */}
      <div
        className="absolute inset-0 rounded-xl pointer-events-none opacity-0 group-hover:opacity-100 transition-opacity duration-300"
        style={{ background: `radial-gradient(circle at 0 0, ${accent}14 0%, transparent 70%)` }}
      />
      <div className="flex items-center justify-between relative">
        <div
          className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
          style={{ background: `${accent}18`, color: accent }}
        >
          {icon}
        </div>
        {trend && (
          <span className="text-[0.68rem] text-[var(--text-muted)] flex items-center gap-0.5">
            <ArrowUpRight className="w-3 h-3" />{trend}
          </span>
        )}
      </div>
      <div className="relative">
        <div className="text-[2rem] font-bold leading-none tracking-tight" style={{ color: accent }}>
          {value}
        </div>
        <div className="text-[0.72rem] font-semibold text-[var(--text-muted)] uppercase tracking-wider mt-1.5">{label}</div>
        {sub && <div className="text-xs text-[var(--text-secondary)] mt-0.5">{sub}</div>}
      </div>
    </div>
  );
}

const ACTIVITY_ICONS: Record<string, string> = {
  error: "🔴",
  warning: "🟡",
  info: "🔵",
  success: "🟢",
};

export default function DashboardPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ["dashboard"],
    queryFn: getDashboard,
  });

  const err = error as (Error & { status?: number }) | null;

  return (
    <div>
      <PageHeader title="Dashboard" subtitle="Echtzeit-Übersicht der PCWächter-Infrastruktur" />

      {err && <div className="mb-5"><ErrorBanner message={err.message} status={err.status} /></div>}

      <div className="grid grid-cols-2 gap-4 mb-6 lg:grid-cols-4">
        {isLoading ? (
          Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="rounded-xl p-5 h-[120px]" style={{ background: "var(--bg-card)", border: "1px solid rgba(255,255,255,0.05)" }}>
              <Skeleton className="h-full w-full" style={{ background: "rgba(255,255,255,0.03)" }} />
            </div>
          ))
        ) : data ? (
          <>
            <KpiCard
              label="Geräte gesamt"
              value={data.kpis.totalDevices}
              sub={`${data.kpis.onlineDevices} gerade online`}
              icon={<Monitor className="w-4 h-4" />}
              accent="#7c5cfc"
            />
            <KpiCard
              label="Online"
              value={data.kpis.onlineDevices}
              icon={<Activity className="w-4 h-4" />}
              accent="#10d9a0"
            />
            <KpiCard
              label="Telemetrie 24h"
              value={data.kpis.telemetry24h}
              icon={<Radio className="w-4 h-4" />}
              accent="#38c0ff"
            />
            <KpiCard
              label="Lizenzen aktiv"
              value={data.kpis.activeLicenses}
              sub={`von ${data.kpis.totalLicenses} gesamt`}
              icon={<KeyRound className="w-4 h-4" />}
              accent="#f59e42"
            />
          </>
        ) : null}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Letzte Aktivitäten</CardTitle>
          {data?.recentActivity?.length ? (
            <span className="text-xs text-[var(--text-muted)]">{data.recentActivity.length} Einträge</span>
          ) : null}
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex flex-col gap-3">
              {Array.from({ length: 5 }).map((_, i) => (
                <Skeleton key={i} className="h-10 w-full" style={{ background: "rgba(255,255,255,0.03)" }} />
              ))}
            </div>
          ) : !data?.recentActivity?.length ? (
            <p className="text-sm text-[var(--text-muted)] py-10 text-center">Keine Aktivitäten vorhanden</p>
          ) : (
            <div className="flex flex-col">
              {data.recentActivity.map((a, idx) => (
                <div
                  key={a.id}
                  className="flex items-start justify-between py-3 gap-4 transition-colors"
                  style={{
                    borderTop: idx > 0 ? "1px solid rgba(255,255,255,0.03)" : undefined,
                  }}
                >
                  <div className="flex items-start gap-3 flex-1 min-w-0">
                    <span className="text-sm mt-0.5 shrink-0">{ACTIVITY_ICONS[a.severity ?? "info"] ?? "⚪"}</span>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm text-[var(--text-primary)] font-medium truncate">{a.action}</p>
                      {a.description && (
                        <p className="text-xs text-[var(--text-muted)] mt-0.5 truncate">{a.description}</p>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    {a.category && (
                      <Badge variant="neutral">{a.category}</Badge>
                    )}
                    <span className="text-xs text-[var(--text-muted)] whitespace-nowrap">{formatRelative(a.timestamp)}</span>
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
