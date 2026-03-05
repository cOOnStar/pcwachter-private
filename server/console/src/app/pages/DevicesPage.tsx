import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { getDevices, type Device } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import DataTable, { type Column } from "../components/shared/DataTable";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { formatRelative } from "@/lib/utils";

const PAGE_SIZE = 50;

function StatusDot({ online }: { online: boolean }) {
  return (
    <span
      className={`inline-block w-2 h-2 rounded-full ${online ? "bg-[var(--success)] shadow-[0_0_6px_var(--success)]" : "bg-[#444]"}`}
    />
  );
}

export default function DevicesPage() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [offset, setOffset] = useState(0);

  const { data, isLoading, error } = useQuery({
    queryKey: ["devices", search, status, offset],
    queryFn: () => getDevices({ search: search || undefined, status: status === "all" ? undefined : status, limit: PAGE_SIZE, offset }),
  });

  const err = error as (Error & { status?: number }) | null;

  const columns: Column<Device>[] = [
    {
      key: "status",
      header: "Status",
      render: (d) => <StatusDot online={d.online} />,
      className: "w-12",
    },
    {
      key: "hostname",
      header: "Hostname",
      render: (d) => (
        <Link to={`/devices/${d.id}`} className="font-medium text-[var(--text-primary)] hover:text-[var(--accent)] transition-colors">
          {d.hostname || "–"}
        </Link>
      ),
    },
    { key: "os", header: "Betriebssystem", render: (d) => d.os || "–" },
    { key: "agent", header: "Agent", render: (d) => <span className="font-mono text-xs">{d.agent || "–"}</span> },
    {
      key: "desktopVersion",
      header: "Desktop",
      render: (d) => <span className="font-mono text-xs">{d.desktopVersion || "–"}</span>,
    },
    {
      key: "updaterVersion",
      header: "Updater",
      render: (d) => <span className="font-mono text-xs">{d.updaterVersion || "–"}</span>,
    },
    { key: "ip", header: "IP", render: (d) => <span className="font-mono text-xs">{d.ip || "–"}</span> },
    {
      key: "blocked",
      header: "",
      render: (d) => d.blocked ? <Badge variant="danger">Gesperrt</Badge> : null,
      className: "w-24",
    },
    {
      key: "lastSeen",
      header: "Zuletzt gesehen",
      render: (d) => <span className="text-xs text-[var(--text-muted)]">{formatRelative(d.lastSeen)}</span>,
    },
  ];

  return (
    <div>
      <PageHeader title="Geräte" subtitle="Alle registrierten Installationen" />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <Card>
        <CardHeader>
          <CardTitle>Geräte ({data?.total ?? 0})</CardTitle>
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
            searchPlaceholder="Hostname oder IP suchen…"
            rowKey={(d) => d.id}
            filters={
              <Select value={status} onValueChange={(v) => { setStatus(v); setOffset(0); }}>
                <SelectTrigger className="w-36">
                  <SelectValue placeholder="Alle Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Alle Status</SelectItem>
                  <SelectItem value="online">Online</SelectItem>
                  <SelectItem value="offline">Offline</SelectItem>
                </SelectContent>
              </Select>
            }
          />
        </CardContent>
      </Card>
    </div>
  );
}
