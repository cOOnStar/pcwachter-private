import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getAuditLog, type AuditLogItem } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { formatDateTime } from "@/lib/utils";

const PAGE_SIZE = 100;

export default function AuditLogPage() {
  const [offset, setOffset] = useState(0);

  const { data, isLoading, error } = useQuery({
    queryKey: ["audit-log", offset],
    queryFn: () => getAuditLog({ limit: PAGE_SIZE, offset }),
  });

  const err = error as (Error & { status?: number }) | null;

  const columns: Column<AuditLogItem>[] = [
    { key: "time", header: "Zeit", render: (a) => <span className="font-mono text-xs">{formatDateTime(a.time)}</span> },
    { key: "actor", header: "Akteur", render: (a) => <span className="font-medium text-[var(--text-primary)]">{a.actor || "System"}</span> },
    { key: "action", header: "Aktion", render: (a) => a.action },
    { key: "target", header: "Ziel", render: (a) => <span className="text-xs text-[var(--text-muted)] truncate max-w-[160px] block">{a.target || "–"}</span> },
    { key: "ip", header: "IP", render: (a) => <span className="font-mono text-xs">{a.ip || "–"}</span> },
    {
      key: "result",
      header: "Ergebnis",
      render: (a) => (
        <Badge variant={a.result === "success" ? "success" : a.result === "denied" ? "danger" : "neutral"}>
          {a.result}
        </Badge>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="Audit Log" subtitle="Alle Aktionen und Änderungen im System" />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <Card>
        <CardHeader>
          <CardTitle>Einträge ({data?.total ?? 0})</CardTitle>
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
            rowKey={(a) => a.id}
          />
        </CardContent>
      </Card>
    </div>
  );
}
