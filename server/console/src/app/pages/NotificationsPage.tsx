import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell } from "lucide-react";
import { getNotifications, markNotificationRead } from "../services/api-service";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import PageHeader from "../components/shared/PageHeader";
import ErrorBanner from "../components/shared/ErrorBanner";
import EmptyState from "../components/shared/EmptyState";
import { formatRelative } from "@/lib/utils";
import { toast } from "@/components/ui/use-toast";

function severityVariant(s: string): "danger" | "warning" | "info" | "neutral" {
  const map: Record<string, "danger" | "warning" | "info" | "neutral"> = {
    critical: "danger", error: "danger", warning: "warning", info: "info",
  };
  return map[s] ?? "neutral";
}

export default function NotificationsPage() {
  const queryClient = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ["notifications"],
    queryFn: getNotifications,
  });

  const err = error as (Error & { status?: number }) | null;

  async function handleRead(id: string) {
    try {
      await markNotificationRead(id);
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
    } catch (e: unknown) {
      toast({ title: "Fehler", description: String(e), variant: "destructive" });
    }
  }

  const items = data?.items ?? [];

  return (
    <div>
      <PageHeader title="Benachrichtigungen" subtitle="Systemmeldungen und Warnungen" />

      {err && <div className="mb-4"><ErrorBanner message={err.message} status={err.status} /></div>}

      <Card>
        <CardHeader>
          <CardTitle>Benachrichtigungen ({data?.total ?? 0})</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex flex-col gap-3">
              {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-16 w-full" />)}
            </div>
          ) : items.length === 0 ? (
            <EmptyState message="Keine Benachrichtigungen" icon={<Bell className="w-10 h-10 opacity-40" />} />
          ) : (
            <div className="flex flex-col divide-y divide-[var(--border-muted)]">
              {items.map((n) => (
                <div
                  key={n.id}
                  className={`flex items-start justify-between py-4 gap-4 ${n.read ? "opacity-60" : ""}`}
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <Badge variant={severityVariant(n.severity)}>{n.severity}</Badge>
                      <span className="text-sm font-medium text-[var(--text-primary)]">{n.title}</span>
                      {!n.read && (
                        <span className="w-2 h-2 rounded-full bg-[var(--accent)] shrink-0" />
                      )}
                    </div>
                    <p className="text-xs text-[var(--text-secondary)]">{n.message}</p>
                    <p className="text-xs text-[var(--text-muted)] mt-1">{formatRelative(n.timestamp)}</p>
                  </div>
                  {!n.read && (
                    <Button variant="ghost" size="sm" onClick={() => handleRead(n.id)}>
                      Als gelesen markieren
                    </Button>
                  )}
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
