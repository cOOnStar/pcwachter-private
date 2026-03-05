import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Activity } from "lucide-react";
import { getActivityFeed, type ActivityItem } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import EmptyState from "../components/shared/EmptyState";
import { formatDateTime } from "@/lib/utils";

const PAGE_SIZE = 50;

function typeVariant(type: string): "info" | "warning" | "danger" | "neutral" {
  const map: Record<string, "info" | "warning" | "danger" | "neutral"> = {
    device: "info",
    license: "neutral",
    account: "info",
    alert: "warning",
    error: "danger",
  };
  return map[type] ?? "neutral";
}

export default function ActivityFeedPage() {
  const [offset, setOffset] = useState(0);

  const { data, isLoading, error } = useQuery({
    queryKey: ["activity-feed", offset],
    queryFn: () => getActivityFeed({ limit: PAGE_SIZE, offset }),
  });

  const err = error as (Error & { status?: number }) | null;

  const columns: Column<ActivityItem>[] = [
    {
      key: "timestamp",
      header: "Zeit",
      render: (a) => <span className="font-mono text-xs">{formatDateTime(a.timestamp)}</span>,
    },
    {
      key: "type",
      header: "Typ",
      render: (a) => <Badge variant={typeVariant(a.type)}>{a.type}</Badge>,
    },
    {
      key: "action",
      header: "Aktion",
      render: (a) => <span className="text-sm text-[var(--text-primary)]">{a.action}</span>,
    },
    {
      key: "target",
      header: "Ziel",
      render: (a) => (
        <span className="text-xs text-[var(--text-muted)] truncate max-w-[160px] block">{a.target || "–"}</span>
      ),
    },
    {
      key: "user",
      header: "Benutzer",
      render: (a) => <span className="text-xs">{a.user || "–"}</span>,
    },
    {
      key: "description",
      header: "Beschreibung",
      render: (a) => (
        <span className="text-xs text-[var(--text-secondary)] truncate max-w-[240px] block">
          {a.description || "–"}
        </span>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="Activity Feed" subtitle="Systemweite Aktivitäten und Ereignisse" />

      {err && (
        <div className="mb-4">
          <ErrorBanner message={err.message} status={err.status} />
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Einträge ({data?.total ?? 0})</CardTitle>
        </CardHeader>
        <CardContent>
          {!isLoading && (data?.items ?? []).length === 0 ? (
            <EmptyState message="Keine Aktivitäten vorhanden" icon={<Activity className="w-10 h-10 opacity-40" />} />
          ) : (
            <DataTable
              columns={columns}
              data={data?.items ?? []}
              total={data?.total ?? 0}
              loading={isLoading}
              pageSize={PAGE_SIZE}
              offset={offset}
              onPageChange={setOffset}
              rowKey={(a) => a.id}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
