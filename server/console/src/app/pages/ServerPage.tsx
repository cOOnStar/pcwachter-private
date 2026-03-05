import { useQuery } from "@tanstack/react-query";
import { getContainers, getHostInfo } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table";

function ProgressBar({ percent }: { percent: number }) {
  const color = percent > 85 ? "var(--danger)" : percent > 65 ? "var(--warning)" : "var(--success)";
  return (
    <div className="bg-[var(--bg-base)] rounded-full h-1.5 w-full overflow-hidden mt-2">
      <div style={{ width: `${Math.min(100, percent)}%`, background: color }} className="h-full transition-all" />
    </div>
  );
}

function formatUptime(seconds: number): string {
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return `${d}d ${h}h ${m}m`;
}

export default function ServerPage() {
  const { data: host, isLoading: hostLoading, error: hostError } = useQuery({
    queryKey: ["host-info"],
    queryFn: getHostInfo,
    refetchInterval: 30_000,
  });

  const { data: containersData, isLoading: ctLoading } = useQuery({
    queryKey: ["containers"],
    queryFn: getContainers,
    refetchInterval: 30_000,
  });

  const err = hostError as (Error & { status?: number }) | null;
  const containers = containersData?.containers ?? [];

  return (
    <div>
      <PageHeader title="Server" subtitle="Host-Ressourcen und Docker-Container" />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <div className="grid grid-cols-1 gap-3 mb-4 md:grid-cols-3">
        {hostLoading ? (
          Array.from({ length: 3 }).map((_, i) => (
            <Card key={i}><Skeleton className="h-20 w-full" /></Card>
          ))
        ) : host ? (
          <>
            <Card>
              <CardHeader><CardTitle>CPU</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-[var(--text-primary)]">{host.cpu_percent.toFixed(1)}%</div>
                <ProgressBar percent={host.cpu_percent} />
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle>Arbeitsspeicher</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-[var(--text-primary)]">{host.memory.percent.toFixed(1)}%</div>
                <ProgressBar percent={host.memory.percent} />
                <p className="text-xs text-[var(--text-muted)] mt-2">{(host.memory.used_mb / 1024).toFixed(1)} / {(host.memory.total_mb / 1024).toFixed(1)} GB</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle>Festplatte</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-[var(--text-primary)]">{host.disk.percent.toFixed(1)}%</div>
                <ProgressBar percent={host.disk.percent} />
                <p className="text-xs text-[var(--text-muted)] mt-2">{host.disk.used_gb.toFixed(1)} / {host.disk.total_gb.toFixed(1)} GB</p>
              </CardContent>
            </Card>
          </>
        ) : null}
      </div>

      {host && (
        <Card className="mb-4">
          <CardContent className="pt-4">
            <div className="flex gap-6">
              <div>
                <p className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-wider">Uptime</p>
                <p className="text-sm text-[var(--text-primary)] mt-0.5">{formatUptime(host.uptime_seconds)}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Docker Container ({containers.length})</CardTitle>
        </CardHeader>
        <CardContent>
          {ctLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : containers.length === 0 ? (
            <p className="text-sm text-[var(--text-muted)] py-8 text-center">Keine Container gefunden</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Image</TableHead>
                  <TableHead>CPU %</TableHead>
                  <TableHead>Memory</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {containers.map((c) => (
                  <TableRow key={c.name}>
                    <TableCell className="font-medium text-[var(--text-primary)]">{c.name}</TableCell>
                    <TableCell>
                      <Badge variant={c.status.includes("running") ? "success" : "warning"}>{c.status}</Badge>
                    </TableCell>
                    <TableCell className="font-mono text-xs">{c.image}</TableCell>
                    <TableCell>{c.cpuPercent.toFixed(2)}%</TableCell>
                    <TableCell>{c.memoryMb.toFixed(1)} MB</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
